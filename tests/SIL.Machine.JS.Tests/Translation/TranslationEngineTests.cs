﻿using System;
using System.Linq;
using Bridge.Html5;
using Bridge.QUnit;
using SIL.Machine.Tokenization;
using SIL.Machine.Web;

namespace SIL.Machine.Translation
{
	public static class TranslationEngineTests
	{
		[Ready]
		public static void RunTests()
		{
			QUnit.Module(nameof(TranslationEngineTests));

			QUnit.Test(nameof(TranslateInteractively_Success_ReturnsSession), TranslateInteractively_Success_ReturnsSession);
			QUnit.Test(nameof(TranslateInteractively_Error_ReturnsNull), TranslateInteractively_Error_ReturnsNull);
			QUnit.Test(nameof(TranslateInteractively_NoRuleResult_ReturnsSession), TranslateInteractively_NoRuleResult_ReturnsSession);
		}

		private static void TranslateInteractively_Success_ReturnsSession(Assert assert)
		{
			var tokenizer = new LatinWordTokenizer();
			var httpClient = new MockHttpClient();
			dynamic json = new
			{
				wordGraph = new
				{
					initialStateScore = -111.111,
					finalStates = new [] {4},
					arcs = new[]
					{
						new
						{
							prevState = 0,
							nextState = 1,
							score = -11.11,
							words = new[] {"This", "is"},
							confidences = new[] {0.4, 0.5},
							sourceStartIndex = 0,
							sourceEndIndex = 1,
							isUnknown = false,
							alignment = new[]
							{
								new {sourceIndex = 0, targetIndex = 0},
								new {sourceIndex = 1, targetIndex = 1}
							}
						},
						new
						{
							prevState = 1,
							nextState = 2,
							score = -22.22,
							words = new[] {"a"},
							confidences = new[] {0.6},
							sourceStartIndex = 2,
							sourceEndIndex = 2,
							isUnknown = false,
							alignment = new[]
							{
								new {sourceIndex = 0, targetIndex = 0}
							}
						},
						new
						{
							prevState = 2,
							nextState = 3,
							score = 33.33,
							words = new[] {"prueba"},
							confidences = new[] {0.0},
							sourceStartIndex = 3,
							sourceEndIndex = 3,
							isUnknown = true,
							alignment = new[]
							{
								new {sourceIndex = 0, targetIndex = 0}
							}
						},
						new
						{
							prevState = 3,
							nextState = 4,
							score = -44.44,
							words = new[] {"."},
							confidences = new[] {0.7},
							sourceStartIndex = 4,
							sourceEndIndex = 4,
							isUnknown = false,
							alignment = new[]
							{
								new {sourceIndex = 0, targetIndex = 0}
							}
						}
					}
				},
				ruleResult = new
				{
					target = new[] {"Esto", "es", "una", "test", "."},
					confidences = new[] {0.0, 0.0, 0.0, 1.0, 0.0},
					sources = new[] {TranslationSources.None, TranslationSources.None, TranslationSources.None, TranslationSources.Transfer, TranslationSources.None},
					alignment = new[]
					{
						new {sourceIndex = 0, targetIndex = 0},
						new {sourceIndex = 1, targetIndex = 1},
						new {sourceIndex = 2, targetIndex = 2},
						new {sourceIndex = 3, targetIndex = 3},
						new {sourceIndex = 4, targetIndex = 4}
					}
				}
			};
			httpClient.Requests.Add(new MockRequest
				{
					Method = HttpRequestMethod.Post,
					ResponseText = JSON.Stringify(json)
				});

			var engine = new TranslationEngine("http://localhost/", "es", "en", "project1", tokenizer, tokenizer, httpClient);
			Action done = assert.Async();
			engine.TranslateInteractively("Esto es una prueba.", 0.2, session =>
				{
					assert.NotEqual(session, null);

					WordGraph wordGraph = session.SmtWordGraph;
					assert.Equal(wordGraph.InitialStateScore, -111.111);
					assert.DeepEqual(wordGraph.FinalStates.ToArray(), new[] {4});
					assert.Equal(wordGraph.Arcs.Count, 4);
					WordGraphArc arc = wordGraph.Arcs[0];
					assert.Equal(arc.PrevState, 0);
					assert.Equal(arc.NextState, 1);
					assert.Equal(arc.Score, -11.11);
					assert.DeepEqual(arc.Words.ToArray(), new[] {"This", "is"});
					assert.DeepEqual(arc.WordConfidences.ToArray(), new[] {0.4, 0.5});
					assert.Equal(arc.SourceStartIndex, 0);
					assert.Equal(arc.SourceEndIndex, 1);
					assert.Equal(arc.IsUnknown, false);
					assert.Equal(arc.Alignment[0, 0], AlignmentType.Aligned);
					assert.Equal(arc.Alignment[1, 1], AlignmentType.Aligned);
					arc = wordGraph.Arcs[2];
					assert.Equal(arc.IsUnknown, true);

					TranslationResult ruleResult = session.RuleResult;
					assert.DeepEqual(ruleResult.TargetSegment.ToArray(), new[] {"Esto", "es", "una", "test", "."});
					assert.DeepEqual(ruleResult.TargetWordConfidences.ToArray(), new[] {0.0, 0.0, 0.0, 1.0, 0.0});
					assert.DeepEqual(ruleResult.TargetWordSources.ToArray(),
						new[] {TranslationSources.None, TranslationSources.None, TranslationSources.None, TranslationSources.Transfer, TranslationSources.None});
					assert.Equal(ruleResult.Alignment[0, 0], AlignmentType.Aligned);
					assert.Equal(ruleResult.Alignment[1, 1], AlignmentType.Aligned);
					assert.Equal(ruleResult.Alignment[2, 2], AlignmentType.Aligned);
					assert.Equal(ruleResult.Alignment[3, 3], AlignmentType.Aligned);
					assert.Equal(ruleResult.Alignment[4, 4], AlignmentType.Aligned);
					done();
				});
		}

		private static void TranslateInteractively_Error_ReturnsNull(Assert assert)
		{
			var tokenizer = new LatinWordTokenizer();
			var httpClient = new MockHttpClient();
			httpClient.Requests.Add(new MockRequest
				{
					Method = HttpRequestMethod.Post,
					ErrorStatus = 404
				});

			var engine = new TranslationEngine("http://localhost/", "es", "en", "project1", tokenizer, tokenizer, httpClient);
			Action done = assert.Async();
			engine.TranslateInteractively("Esto es una prueba.", 0.2, session =>
				{
					assert.Equal(session, null);
					done();
				});
		}

		private static void TranslateInteractively_NoRuleResult_ReturnsSession(Assert assert)
		{
			var tokenizer = new LatinWordTokenizer();
			var httpClient = new MockHttpClient();
			dynamic json = new
			{
				wordGraph = new
				{
					initialStateScore = -111.111,
					finalStates = new string[0],
					arcs = new DOMStringList[0]
				},
				ruleResult = (string) null
			};
			httpClient.Requests.Add(new MockRequest
				{
					Method = HttpRequestMethod.Post,
					ResponseText = JSON.Stringify(json)
				});

			var engine = new TranslationEngine("http://localhost/", "es", "en", "project1", tokenizer, tokenizer, httpClient);
			Action done = assert.Async();
			engine.TranslateInteractively("Esto es una prueba.", 0.2, session =>
				{
					assert.NotEqual(session, null);
					assert.NotEqual(session.SmtWordGraph, null);
					assert.Equal(session.RuleResult, null);
					done();
				});
		}
	}
}
