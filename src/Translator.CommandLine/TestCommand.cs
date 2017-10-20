﻿using McMaster.Extensions.CommandLineUtils;
using SIL.CommandLine;
using SIL.Machine.Corpora;
using SIL.Machine.Translation.Thot;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SIL.Machine.Translation
{
	public class TestCommand : EngineCommandBase
	{
		private readonly CommandOption _confidenceOption;
		private readonly CommandOption _traceOption;
		private readonly CommandOption _nOption;

		private int _actionCount;
		private int _charCount;
		private int _correctSuggestionCount;
		private int _totalSuggestionCount;

		public TestCommand()
			: base(false)
		{
			Name = "test";

			_confidenceOption = Option("-c|--confidence <percentage>", "The confidence threshold.",
				CommandOptionType.SingleValue);
			_nOption = Option("-n <number>", "The number of suggestions to generate.",
				CommandOptionType.SingleValue);
			_traceOption = Option("--trace <path>", "The trace output directory.",
				CommandOptionType.SingleValue);
		}

		protected override int ExecuteCommand()
		{
			int code = base.ExecuteCommand();
			if (code != 0)
				return code;

			if (!File.Exists(EngineConfigFileName))
			{
				Out.WriteLine("The specified engine directory is invalid.");
				return 1;
			}

			double confidence = 0.2;
			if (_confidenceOption.HasValue())
			{
				if (!double.TryParse(_confidenceOption.Value(), out confidence))
				{
					Out.WriteLine("The specified confidence is invalid.");
					return 1;
				}
			}

			int n = 1;
			if (_nOption.HasValue())
			{
				if (!int.TryParse(_nOption.Value(), out n))
				{
					Out.WriteLine("The specified number of suggestions is invalid.");
					return 1;
				}
			}

			if (_traceOption.HasValue())
			{
				if (!Directory.Exists(_traceOption.Value()))
					Directory.CreateDirectory(_traceOption.Value());
			}

			var suggester = new PhraseTranslationSuggester(confidence);

			var corpus = new ParallelTextCorpus(SourceCorpus, TargetCorpus);
			int totalSegmentCount = corpus.Texts.SelectMany(t => t.Segments).Count(s => !s.IsEmpty);

			Stopwatch watch = Stopwatch.StartNew();
			Out.Write("Testing... ");
			int segmentCount = 0;
			using (var progress = new ConsoleProgressBar(Out))
			using (IInteractiveSmtModel smtModel = new ThotSmtModel(EngineConfigFileName))
			using (IInteractiveSmtEngine engine = smtModel.CreateInteractiveEngine())
			{
				foreach (ParallelText text in corpus.Texts)
				{
					using (StreamWriter traceWriter = CreateTraceWriter(text))
					{
						foreach (ParallelTextSegment segment in text.Segments.Where(s => !s.IsEmpty))
						{
							TestSegment(engine, suggester, n, segment, traceWriter);
							segmentCount++;
							progress.Report((double) segmentCount / totalSegmentCount);
						}
					}
				}
			}
			Out.WriteLine("done.");
			watch.Stop();

			Out.WriteLine($"Execution time: {watch.Elapsed:c}");
			Out.WriteLine($"# of Segments: {segmentCount}");
			Out.WriteLine($"# of Suggestions: {_totalSuggestionCount}");
			Out.WriteLine($"# of Correct Suggestions: {_correctSuggestionCount}");
			double ksmr = (double) _actionCount / _charCount;
			Out.WriteLine($"KSMR: {ksmr:0.00}");
			double precision = (double) _correctSuggestionCount / _totalSuggestionCount;
			Out.WriteLine($"Precision: {precision:0.00}");
			return 0;
		}

		private StreamWriter CreateTraceWriter(ParallelText text)
		{
			if (_traceOption.HasValue())
			{
				string fileName = Path.Combine(_traceOption.Value(), text.Id + "-trace.txt");
				return new StreamWriter(fileName);
			}

			return null;
		}

		private void TestSegment(IInteractiveSmtEngine engine, ITranslationSuggester suggester, int n,
			ParallelTextSegment segment, StreamWriter traceWriter)
		{
			traceWriter?.WriteLine($"Segment:      {segment.SegmentRef}");
			string[] sourceSegment = segment.SourceSegment.Select(Preprocessors.Lowercase).ToArray();
			traceWriter?.WriteLine($"Source:       {string.Join(" ", sourceSegment)}");
			string[] targetSegment = segment.TargetSegment.Select(Preprocessors.Lowercase).ToArray();
			traceWriter?.WriteLine($"Target:       {string.Join(" ", targetSegment)}");
			traceWriter?.WriteLine(new string('=', 120));
			string[][] prevSuggestionWords = null;
			bool isLastWordSuggestion = false;
			string suggestionResult = null;
			using (IInteractiveTranslationSession session = engine.TranslateInteractively(n, sourceSegment))
			{
				while (session.Prefix.Count < targetSegment.Length || !session.IsLastWordComplete)
				{
					int targetIndex = session.Prefix.Count;
					if (!session.IsLastWordComplete)
						targetIndex--;

					bool match = false;
					IReadOnlyList<IReadOnlyList<int>> suggestions = suggester.GetSuggestedWordIndices(session);
					string[][] suggestionWords = suggestions.Select((s, k) =>
						s.Select(j => session.CurrentResults[k].TargetSegment[j]).ToArray()).ToArray();
					if (prevSuggestionWords == null || !SuggestionsAreEqual(prevSuggestionWords, suggestionWords))
					{
						WritePrefix(traceWriter, suggestionResult, session.Prefix);
						WriteSuggestions(traceWriter, session, suggestions);
						suggestionResult = null;
						if (suggestions.Any(s => s.Count > 0))
							_totalSuggestionCount++;
					}
					for (int k = 0; k < suggestions.Count; k++)
					{
						var accepted = new List<int>();
						for (int i = 0, j = targetIndex; i < suggestionWords[k].Length && j < targetSegment.Length; i++)
						{
							if (suggestionWords[k][i] == targetSegment[j])
							{
								accepted.Add(suggestions[k][i]);
								j++;
							}
							else if (accepted.Count == 0)
							{
								j = targetIndex;
							}
							else
							{
								break;
							}
						}

						if (accepted.Count > 0)
						{
							session.AppendSuggestionToPrefix(k, accepted);
							isLastWordSuggestion = true;
							_actionCount++;
							_correctSuggestionCount++;
							if (accepted.Count == suggestions[k].Count)
								suggestionResult = "ACCEPT_FULL";
							else if (accepted[0] == suggestions[k][0])
								suggestionResult = "ACCEPT_INIT";
							else if (accepted[accepted.Count - 1] == suggestions[k][suggestions[k].Count - 1])
								suggestionResult = "ACCEPT_FIN";
							else
								suggestionResult = "ACCEPT_MID";

							match = true;
							break;
						}
					}

					if (!match)
					{
						if (isLastWordSuggestion)
						{
							_actionCount++;
							isLastWordSuggestion = false;
							WritePrefix(traceWriter, suggestionResult, session.Prefix);
							suggestionResult = null;
						}

						int len = session.IsLastWordComplete ? 0 : session.Prefix[session.Prefix.Count - 1].Length;
						string targetWord = targetSegment[targetIndex];
						if (len == targetWord.Length)
						{
							session.AppendToPrefix("", true);
						}
						else
						{
							string c = targetWord.Substring(len, 1);
							session.AppendToPrefix(c, false);
						}

						suggestionResult = suggestions.Any(s => s.Count > 0) ? "REJECT" : "NONE";
						_actionCount++;
					}

					prevSuggestionWords = suggestionWords;
				}

				WritePrefix(traceWriter, suggestionResult, session.Prefix);

				session.Approve();
			}

			_charCount += targetSegment.Sum(w => w.Length + 1);
			traceWriter?.WriteLine();
		}

		private void WritePrefix(StreamWriter traceWriter, string suggestionResult, IReadOnlyList<string> prefix)
		{
			if (traceWriter == null || suggestionResult == null)
				return;

			traceWriter.Write(("-" + suggestionResult).PadRight(14));
			traceWriter.WriteLine(string.Join(" ", prefix));
		}

		private void WriteSuggestions(StreamWriter traceWriter, IInteractiveTranslationSession session,
			IReadOnlyList<IReadOnlyList<int>> suggestions)
		{
			if (traceWriter == null)
				return;

			for (int k = 0; k < suggestions.Count; k++)
			{
				bool inSuggestion = false;
				traceWriter.Write($"SUGGESTION {k + 1}  ");
				for (int j = 0; j < session.CurrentResults[k].TargetSegment.Count; j++)
				{
					if (suggestions[k].Contains(j))
					{
						if (j > 0)
							traceWriter.Write(" ");
						if (!inSuggestion)
							traceWriter.Write("[");
						inSuggestion = true;
					}
					else if (inSuggestion)
					{
						traceWriter.Write("] ");
						inSuggestion = false;
					}
					else if (j > 0)
					{
						traceWriter.Write(" ");
					}

					traceWriter.Write(session.CurrentResults[k].TargetSegment[j]);
				}
				if (inSuggestion)
					traceWriter.Write("]");
				traceWriter.WriteLine();
			}
		}

		private bool SuggestionsAreEqual(string[][] x, string[][] y)
		{
			if (x.Length != y.Length)
				return false;

			for (int i = 0; i < x.Length; i++)
			{
				if (!x[i].SequenceEqual(y[i]))
					return false;
			}

			return true;
		}
	}
}
