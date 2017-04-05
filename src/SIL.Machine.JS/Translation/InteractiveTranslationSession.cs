﻿using System;
using System.Linq;
using System.Threading.Tasks;
using SIL.Machine.Annotations;

namespace SIL.Machine.Translation
{
	public class InteractiveTranslationSession
	{
		private const double RuleEngineThreshold = 0.05;

		private readonly TranslationEngine _engine;
		private readonly ErrorCorrectionWordGraphProcessor _wordGraphProcessor;
		private TranslationResult _curResult;
		private double _confidenceThreshold;

		internal InteractiveTranslationSession(TranslationEngine engine, string[] sourceSegment, double confidenceThreshold, WordGraph smtWordGraph,
			TranslationResult ruleResult)
		{
			_engine = engine;
			SourceSegment = sourceSegment;
			_confidenceThreshold = confidenceThreshold;
			RuleResult = ruleResult;
			SmtWordGraph = smtWordGraph;

			_wordGraphProcessor = new ErrorCorrectionWordGraphProcessor(_engine.ErrorCorrectionModel, SmtWordGraph);
			UpdatePrefix("");
		}

		public WordGraph SmtWordGraph { get; }
		public TranslationResult RuleResult { get; }

		public string[] SourceSegment { get; }

		public double ConfidenceThreshold
		{
			get { return _confidenceThreshold; }
			set
			{
				if (_confidenceThreshold != value)
				{
					_confidenceThreshold = value;
					UpdateSuggestion();
				}
			}
		}

		public string[] Prefix { get; private set; }

		public bool IsLastWordComplete { get; private set; }

		public string[] CurrentSuggestion { get; private set; }

		public string[] UpdatePrefix(string prefix)
		{
			Span<int>[] tokenSpans = _engine.TargetTokenizer.Tokenize(prefix).ToArray();
			Prefix = tokenSpans.Select(s => prefix.Substring(s.Start, s.Length)).ToArray();
			IsLastWordComplete = tokenSpans.Length == 0 || tokenSpans[tokenSpans.Length - 1].End != prefix.Length;

			TranslationInfo correction = _wordGraphProcessor.Correct(Prefix, IsLastWordComplete, 1).FirstOrDefault();
			TranslationResult smtResult = CreateResult(correction);

			if (RuleResult == null)
			{
				_curResult = smtResult;
			}
			else
			{
				int prefixCount = Prefix.Length;
				if (!IsLastWordComplete)
					prefixCount--;

				_curResult = smtResult.Merge(prefixCount, RuleEngineThreshold, RuleResult);
			}

			UpdateSuggestion();

			return CurrentSuggestion;
		}

		private void UpdateSuggestion()
		{
			string[] suggestions = TranslationSuggester.GetSuggestedWordIndices(Prefix, IsLastWordComplete, _curResult, _confidenceThreshold)
				.Select(j => _curResult.TargetSegment[j]).ToArray();

			CurrentSuggestion = suggestions;
		}

		public void Approve(Action<bool> onFinished)
		{
			Task<bool> task = _engine.RestClient.TrainSegmentPairAsync(SourceSegment, Prefix);
			task.ContinueWith(t => onFinished(t.Result));
		}

		private TranslationResult CreateResult(TranslationInfo info)
		{
			if (info == null)
			{
				return new TranslationResult(SourceSegment, Enumerable.Empty<string>(), Enumerable.Empty<double>(),
					Enumerable.Empty<TranslationSources>(), new WordAlignmentMatrix(SourceSegment.Length, 0));
			}

			double[] confidences = info.TargetConfidences.ToArray();
			var sources = new TranslationSources[info.Target.Count];
			var alignment = new WordAlignmentMatrix(SourceSegment.Length, info.Target.Count);
			int trgPhraseStartIndex = 0;
			foreach (PhraseInfo phrase in info.Phrases)
			{
				for (int j = trgPhraseStartIndex; j <= phrase.TargetCut; j++)
				{
					for (int i = phrase.SourceStartIndex; i <= phrase.SourceEndIndex; i++)
					{
						if (phrase.Alignment[i - phrase.SourceStartIndex, j - trgPhraseStartIndex] == AlignmentType.Aligned)
							alignment[i, j] = AlignmentType.Aligned;
					}
					sources[j] = info.TargetUnknownWords.Contains(j) ? TranslationSources.None : TranslationSources.Smt;
				}
				trgPhraseStartIndex = phrase.TargetCut + 1;
			}

			return new TranslationResult(SourceSegment, info.Target, confidences, sources, alignment);
		}
	}
}
