﻿using McMaster.Extensions.CommandLineUtils;
using SIL.Machine.Corpora;

namespace SIL.Machine.Translation
{
	public class CorpusCommand : ParallelTextCorpusCommandBase
	{
		private readonly CommandOption _maxLengthOption;
		private readonly CommandOption _countOption;

		public CorpusCommand()
			: base(true)
		{
			Name = "corpus";

			_countOption = Option("-c|--count", "Only output the # of parallel segments.", CommandOptionType.NoValue);
			_maxLengthOption = Option("--max-seglen <number>", "Maximum segment length.",
				CommandOptionType.SingleValue);
		}

		protected override int ExecuteCommand()
		{
			int code = base.ExecuteCommand();
			if (code != 0)
				return code;

			int parallelCorpusCount = GetParallelCorpusCount();
			if (_countOption.HasValue())
			{
				Out.WriteLine(parallelCorpusCount);
			}
			else
			{
				int maxLength = 100;
				if (_maxLengthOption.HasValue())
				{
					if (!int.TryParse(_maxLengthOption.Value(), out maxLength))
					{
						Out.WriteLine("The specified maximum segment length is invalid.");
						return 1;
					}
				}

				WriteCorpusStats("Source", SourceCorpus, maxLength);
				WriteCorpusStats("Target", TargetCorpus, maxLength);
				Out.WriteLine($"# of Parallel Segments: {parallelCorpusCount}");
			}

			return 0;
		}

		private void WriteCorpusStats(string type, ITextCorpus corpus, int maxLength)
		{
			int textCount = 0;
			int segmentCount = 0;
			int wordCount = 0;
			foreach (IText text in corpus.Texts)
			{
				foreach (TextSegment segment in text.Segments)
				{
					if (segment.Segment.Count > maxLength)
					{
						Out.WriteLine($"{type} segment \"{text.Id} {segment.SegmentRef}\" is too long, " 
							+ $"length: {segment.Segment.Count}");
					}

					wordCount += segment.Segment.Count;
					segmentCount++;
				}

				textCount++;
			}

			Out.WriteLine($"# of {type} Texts: {textCount}");
			Out.WriteLine($"# of {type} Segments: {segmentCount}");
			Out.WriteLine($"# of {type} Words: {wordCount}");
			double avgSegmentLength = (double) wordCount / segmentCount;
			Out.WriteLine($"Avg. {type} Segment Length: {avgSegmentLength:#.##}");
		}
	}
}
