﻿using System;
using System.Collections.Generic;
using Bridge.Html5;
using SIL.Machine.Web;

namespace SIL.Machine.Translation
{
	public class TranslationEngine
	{
		public TranslationEngine(string baseUrl, string sourceLanguageTag, string targetLanguageTag, string projectId)
			: this(baseUrl, sourceLanguageTag, targetLanguageTag, projectId, new AjaxWebClient())
		{
		}

		public TranslationEngine(string baseUrl, string sourceLanguageTag, string targetLanguageTag, string projectId, IWebClient webClient)
		{
			BaseUrl = baseUrl;
			SourceLanguageTag = sourceLanguageTag;
			TargetLanguageTag = targetLanguageTag;
			ProjectId = projectId;
			WebClient = webClient;
			ErrorCorrectionModel = new ErrorCorrectionModel();
		}

		public string SourceLanguageTag { get; }
		public string TargetLanguageTag { get; }
		public string ProjectId { get; }
		public string BaseUrl { get; }
		internal IWebClient WebClient { get; }
		internal ErrorCorrectionModel ErrorCorrectionModel { get; }

		public void TranslateInteractively(string[] sourceSegment, double confidenceThreshold, Action<InteractiveTranslationSession> onFinished)
		{
			string url = string.Format("{0}/translation/engines/{1}/{2}/projects/{3}/actions/interactive-translate", BaseUrl, SourceLanguageTag, TargetLanguageTag, ProjectId);
			string body = JSON.Stringify(sourceSegment);
			WebClient.Send("POST", url, body, "application/json", responseText => onFinished(CreateSession(sourceSegment, confidenceThreshold, JSON.Parse(responseText))),
				status => onFinished(null));
		}

		private InteractiveTranslationSession CreateSession(string[] sourceSegment, double confidenceThreshold, dynamic json)
		{
			WordGraph wordGraph = CreateWordGraph(json["wordGraph"]);
			TranslationResult ruleResult = CreateRuleResult(sourceSegment, json["ruleResult"]);
			return new InteractiveTranslationSession(this, sourceSegment, confidenceThreshold, wordGraph, ruleResult);
		}

		private WordGraph CreateWordGraph(dynamic jsonWordGraph)
		{
			double initialStateScore = jsonWordGraph["initialStateScore"];

			var finalStates = new List<int>();
			var jsonFinalStates = jsonWordGraph["finalStates"];
			foreach (var jsonFinalState in jsonFinalStates)
				finalStates.Add(jsonFinalState);

			var jsonArcs = jsonWordGraph["arcs"];
			var arcs = new List<WordGraphArc>();
			foreach (var jsonArc in jsonArcs)
			{
				int prevState = jsonArc["prevState"];
				int nextState = jsonArc["nextState"];
				double score = jsonArc["score"];

				var jsonWords = jsonArc["words"];
				var words = new List<string>();
				foreach (var jsonWord in jsonWords)
					words.Add(jsonWord);

				var jsonConfidences = jsonArc["confidences"];
				var confidences = new List<double>();
				foreach (var jsonConfidence in jsonConfidences)
					confidences.Add(jsonConfidence);

				int srcStartIndex = jsonArc["sourceStartIndex"];
				int endStartIndex = jsonArc["sourceEndIndex"];
				bool isUnknown = jsonArc["isUnknown"];

				var jsonAlignment = jsonArc["alignment"];
				var alignment = new WordAlignmentMatrix(endStartIndex - srcStartIndex + 1, words.Count);
				foreach (var jsonAligned in jsonAlignment)
				{
					int i = jsonAligned["sourceIndex"];
					int j = jsonAligned["targetIndex"];
					alignment[i, j] = AlignmentType.Aligned;
				}

				arcs.Add(new WordGraphArc(prevState, nextState, score, words.ToArray(), alignment, confidences.ToArray(),
					srcStartIndex, endStartIndex, isUnknown));
			}

			return new WordGraph(arcs, finalStates, initialStateScore);
		}

		private TranslationResult CreateRuleResult(string[] sourceSegment, dynamic jsonResult)
		{
			if (jsonResult == null)
				return null;

			var jsonTarget = jsonResult["target"];
			var targetSegment = new List<string>();
			foreach (var jsonWord in jsonTarget)
				targetSegment.Add(jsonWord);

			var jsonConfidences = jsonResult["confidences"];
			var confidences = new List<double>();
			foreach (var jsonConfidence in jsonConfidences)
				confidences.Add(jsonConfidence);

			var jsonSources = jsonResult["sources"];
			var sources = new List<TranslationSources>();
			foreach (var jsonSource in jsonSources)
				sources.Add((TranslationSources) jsonSource);

			var jsonAlignment = jsonResult["alignment"];
			var alignment = new WordAlignmentMatrix(sourceSegment.Length, targetSegment.Count);
			foreach (var jsonAligned in jsonAlignment)
			{
				int i = jsonAligned["sourceIndex"];
				int j = jsonAligned["targetIndex"];
				alignment[i, j] = AlignmentType.Aligned;
			}

			return new TranslationResult(sourceSegment, targetSegment, confidences, sources, alignment);
		}
	}
}
