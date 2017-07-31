﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SIL.ObjectModel;

namespace SIL.Machine.Translation.Thot
{
	public class ThotSingleWordAlignmentModel : DisposableBase, ISegmentAligner
	{
		private readonly bool _closeOnDispose;
		private readonly string _prefFileName;

		public ThotSingleWordAlignmentModel()
		{
			Handle = Thot.swAlignModel_create();
			_closeOnDispose = true;
		}

		internal ThotSingleWordAlignmentModel(IntPtr handle)
		{
			Handle = handle;
			_closeOnDispose = false;
		}

		public ThotSingleWordAlignmentModel(string prefFileName, bool createNew = false)
		{
			if (!createNew && !File.Exists(prefFileName + ".src"))
				throw new FileNotFoundException("The single-word alignment model configuration could not be found.");

			_prefFileName = prefFileName;
			Handle = createNew || !File.Exists(prefFileName + ".src") ? Thot.swAlignModel_create() : Thot.swAlignModel_open(_prefFileName);
			_closeOnDispose = true;
		}

		internal IntPtr Handle { get; set; }

		public void AddSegmentPair(IReadOnlyList<string> sourceSegment, IReadOnlyList<string> targetSegment, WordAlignmentMatrix hintMatrix = null)
		{
			IntPtr nativeSourceSegment = Thot.ConvertStringsToNativeUtf8(sourceSegment);
			IntPtr nativeTargetSegment = Thot.ConvertStringsToNativeUtf8(targetSegment);
			IntPtr nativeMatrix = IntPtr.Zero;
			uint iLen = 0, jLen = 0;
			if (hintMatrix != null)
			{
				nativeMatrix = Thot.ConvertWordAlignmentMatrixToNativeMatrix(hintMatrix);
				iLen = (uint) hintMatrix.RowCount;
				jLen = (uint) hintMatrix.ColumnCount;
			}

			try
			{
				Thot.swAlignModel_addSentencePair(Handle, nativeSourceSegment, nativeTargetSegment, nativeMatrix, iLen, jLen);
			}
			finally
			{
				Thot.FreeNativeMatrix(nativeMatrix, iLen);
				Marshal.FreeHGlobal(nativeTargetSegment);
				Marshal.FreeHGlobal(nativeSourceSegment);
			}
		}

		public void Train(int iterCount)
		{
			Thot.swAlignModel_train(Handle, (uint) iterCount);
		}

		public void Save()
		{
			if (string.IsNullOrEmpty(_prefFileName))
				throw new InvalidOperationException("This single word alignment model cannot be saved.");
			Thot.swAlignModel_save(Handle, _prefFileName);
		}

		public double GetTranslationProbability(string sourceWord, string targetWord)
		{
			IntPtr nativeSourceWord = Thot.ConvertStringToNativeUtf8(sourceWord ?? "NULL");
			IntPtr nativeTargetWord = Thot.ConvertStringToNativeUtf8(targetWord ?? "NULL");
			try
			{
				return Thot.swAlignModel_getTranslationProbability(Handle, nativeSourceWord, nativeTargetWord);
			}
			finally
			{
				Marshal.FreeHGlobal(nativeTargetWord);
				Marshal.FreeHGlobal(nativeSourceWord);
			}
		}

		public double GetAlignmentProbability(WordAlignmentMatrix matrix, int targetIndex)
		{
			double maxProb = -1;
			foreach (uint prevI in GetSourcePositionIndices(matrix, targetIndex - 1))
			{
				foreach (uint i in GetSourcePositionIndices(matrix, targetIndex))
				{
					
					double prob = Thot.swAlignModel_getAlignmentProbability(Handle, prevI, (uint) matrix.RowCount, i);
					maxProb = Math.Max(maxProb, prob);
				}
			}
			return maxProb;
		}

		private static IEnumerable<uint> GetSourcePositionIndices(WordAlignmentMatrix matrix, int j)
		{
			if (j < 0)
			{
				yield return 0;
			}
			else
			{
				bool aligned = false;
				foreach (int curI in matrix.GetColumnAlignedIndices(j))
				{
					// add 1 to convert the matrix index to a Thot position index, which is 1-based
					yield return (uint) curI + 1;
					aligned = true;
				}
				if (!aligned)
				{
					// check if there are no aligned source indices before this target index
					do
					{
						j--;
					} while (j >= 0 && matrix.IsColumnAligned(j) == AlignmentType.NotAligned);

					if (j < 0)
					{
						yield return 0;
					}
					else
					{
						// if the target index does not align with anything, then return all null position indices
						// [source length + 1, source length * 2]
						for (uint i = 0; i < matrix.RowCount; i++)
							yield return (uint) matrix.RowCount + i + 1;
					}
				}
			}
		}

		public WordAlignmentMatrix GetBestAlignment(IReadOnlyList<string> sourceSegment, IReadOnlyList<string> targetSegment,
			WordAlignmentMatrix hintMatrix = null)
		{
			IntPtr nativeSourceSegment = Thot.ConvertStringsToNativeUtf8(sourceSegment);
			IntPtr nativeTargetSegment = Thot.ConvertStringsToNativeUtf8(targetSegment);
			IntPtr nativeMatrix = hintMatrix == null
				? Thot.AllocNativeMatrix(sourceSegment.Count, targetSegment.Count)
				: Thot.ConvertWordAlignmentMatrixToNativeMatrix(hintMatrix);

			uint iLen = (uint) sourceSegment.Count;
			uint jLen = (uint) targetSegment.Count;
			try
			{
				Thot.swAlignModel_getBestAlignment(Handle, nativeSourceSegment, nativeTargetSegment, nativeMatrix, ref iLen, ref jLen);
				return Thot.ConvertNativeMatrixToWordAlignmentMatrix(nativeMatrix, iLen, jLen);
			}
			finally
			{
				Thot.FreeNativeMatrix(nativeMatrix, iLen);
				Marshal.FreeHGlobal(nativeTargetSegment);
				Marshal.FreeHGlobal(nativeSourceSegment);
			}
		}

		protected override void DisposeUnmanagedResources()
		{
			if (_closeOnDispose)
				Thot.swAlignModel_close(Handle);
		}
	}
}
