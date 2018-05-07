using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SIL.Machine.Corpora;

namespace SIL.Machine.Translation
{
	public enum AlignmentType
	{
		Unknown = -1,
		NotAligned = 0,
		Aligned = 1
	}

	public partial class WordAlignmentMatrix
	{
		private AlignmentType[,] _matrix;

		public WordAlignmentMatrix(int i, int j, AlignmentType defaultValue = AlignmentType.NotAligned)
		{
			_matrix = new AlignmentType[i, j];
			if (defaultValue != AlignmentType.NotAligned)
				SetAll(defaultValue);
		}

		private WordAlignmentMatrix(WordAlignmentMatrix other)
		{
			_matrix = new AlignmentType[other.RowCount, other.ColumnCount];
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
					_matrix[i, j] = other._matrix[i, j];
			}
		}

		public int RowCount => _matrix.GetLength(0);

		public int ColumnCount => _matrix.GetLength(1);

		public void SetAll(AlignmentType value)
		{
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
					_matrix[i, j] = value;
			}
		}

		public AlignmentType this[int i, int j]
		{
			get { return _matrix[i, j]; }
			set { _matrix[i, j] = value; }
		}

		public AlignmentType IsRowAligned(int i)
		{
			for (int j = 0; j < ColumnCount; j++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					return AlignmentType.Aligned;
				if (_matrix[i, j] == AlignmentType.Unknown)
					return AlignmentType.Unknown;
			}
			return AlignmentType.NotAligned;
		}

		public AlignmentType IsColumnAligned(int j)
		{
			for (int i = 0; i < RowCount; i++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					return AlignmentType.Aligned;
				if (_matrix[i, j] == AlignmentType.Unknown)
					return AlignmentType.Unknown;
			}
			return AlignmentType.NotAligned;
		}

		public IEnumerable<int> GetRowAlignedIndices(int i)
		{
			for (int j = 0; j < ColumnCount; j++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					yield return j;
			}
		}

		public IEnumerable<int> GetColumnAlignedIndices(int j)
		{
			for (int i = 0; i < RowCount; i++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					yield return i;
			}
		}

		public bool IsNeighborAligned(int i, int j)
		{
			if (i > 0 && _matrix[i - 1, j] == AlignmentType.Aligned)
				return true;
			if (j > 0 && _matrix[i, j - 1] == AlignmentType.Aligned)
				return true;
			if (i < RowCount - 1 && _matrix[i + 1, j] == AlignmentType.Aligned)
				return true;
			if (j < ColumnCount - 1 && _matrix[i, j + 1] == AlignmentType.Aligned)
				return true;
			return false;
		}

		public void UnionWith(WordAlignmentMatrix other)
		{
			if (RowCount != other.RowCount || ColumnCount != other.ColumnCount)
				throw new ArgumentException("The matrices are not the same size.", nameof(other));

			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
				{
					if (!(_matrix[i, j] == AlignmentType.Aligned || other._matrix[i, j] == AlignmentType.Aligned))
						_matrix[i, j] = AlignmentType.Aligned;
				}
			}
		}

		public void IntersectWith(WordAlignmentMatrix other)
		{
			if (RowCount != other.RowCount || ColumnCount != other.ColumnCount)
				throw new ArgumentException("The matrices are not the same size.", nameof(other));

			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
				{
					if (!(_matrix[i, j] == AlignmentType.Aligned && other._matrix[i, j] == AlignmentType.Aligned))
						_matrix[i, j] = AlignmentType.NotAligned;
				}
			}
		}

		public void SymmetrizeWith(WordAlignmentMatrix other)
		{
			if (RowCount != other.RowCount || ColumnCount != other.ColumnCount)
				throw new ArgumentException("The matrices are not the same size.", nameof(other));

			WordAlignmentMatrix aux = Clone();

			IntersectWith(other);
			WordAlignmentMatrix prev = null;
			while (!ValueEquals(prev))
			{
				prev = Clone();
				for (int i = 0; i < RowCount; i++)
				{
					for (int j = 0; j < ColumnCount; j++)
					{
						if ((other._matrix[i, j] == AlignmentType.Aligned || aux._matrix[i, j] == AlignmentType.Aligned) && _matrix[i, j] == AlignmentType.NotAligned)
						{
							if (IsColumnAligned(j) == AlignmentType.NotAligned && IsRowAligned(i) == AlignmentType.NotAligned)
								_matrix[i, j] = AlignmentType.Aligned;
							else if (IsNeighborAligned(i, j))
								_matrix[i, j] = AlignmentType.Aligned;
						}
					}
				}
			}
		}

		public void Transpose()
		{
			var newMatrix = new AlignmentType[ColumnCount, RowCount];
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
					newMatrix[j, i] = _matrix[i, j];
			}
			_matrix = newMatrix;
		}

		private IEnumerable<AlignedWordPair> GetAlignedWordPairs(out IReadOnlyList<int> sourceIndices,
			out IReadOnlyList<int> targetIndices)
		{
			var source = new int[ColumnCount];
			int[] target = Enumerable.Repeat(-2, RowCount).ToArray();
			var wordPairs = new List<AlignedWordPair>();
			int prev = -1;
			for (int j = 0; j < ColumnCount; j++)
			{
				bool found = false;
				for (int i = 0; i < RowCount; i++)
				{
					if (this[i, j] == AlignmentType.Aligned)
					{
						if (!found)
							source[j] = i;
						if (target[i] == -2)
							target[i] = j;
						wordPairs.Add(new AlignedWordPair(i, j));
						prev = i;
						found = true;
					}
				}

				// unaligned indices
				if (!found)
					source[j] = prev == -1 ? -1 : RowCount + prev;
			}

			// all remaining target indices are unaligned, so fill them in
			prev = -1;
			for (int i = 0; i < RowCount; i++)
			{
				if (target[i] == -2)
					target[i] = prev == -1 ? -1 : ColumnCount + prev;
				else
					prev = target[i];
			}

			sourceIndices = source;
			targetIndices = target;
			return wordPairs;
		}

		public IEnumerable<AlignedWordPair> GetAlignedWordPairs()
		{
			IReadOnlyList<int> sourceIndices;
			IReadOnlyList<int> targetIndices;
			return GetAlignedWordPairs(out sourceIndices, out targetIndices);
		}

		public string ToGizaFormat(IEnumerable<string> sourceSegment, IEnumerable<string> targetSegment)
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{0}\n", string.Join(" ", targetSegment));

			var sourceWords = new List<string> {"NULL"};
			sourceWords.AddRange(sourceSegment);

			int i = 0;
			foreach (string sourceWord in sourceWords)
			{
				if (i > 0)
					sb.Append(" ");
				sb.Append(sourceWord);
				sb.Append(" ({ ");
				for (int j = 0; j < ColumnCount; j++)
				{
					if (i == 0)
					{
						if (IsColumnAligned(j) == AlignmentType.NotAligned)
						{
							sb.Append(j + 1);
							sb.Append(" ");
						}
					}
					else if (_matrix[i - 1, j] == AlignmentType.Aligned)
					{
						sb.Append(j + 1);
						sb.Append(" ");
					}
				}

				sb.Append("})");
				i++;
			}
			sb.Append("\n");
			return sb.ToString();
		}

		public bool ValueEquals(WordAlignmentMatrix other)
		{
			if (other == null)
				return false;

			if (RowCount != other.RowCount || ColumnCount != other.ColumnCount)
				return false;

			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
				{
					if (_matrix[i, j] != other._matrix[i, j])
						return false;
				}
			}
			return true;
		}

		public override string ToString()
		{
			return string.Join(" ", GetAlignedWordPairs().Select(wp => wp.ToString()));
		}

		public WordAlignmentMatrix Clone()
		{
			return new WordAlignmentMatrix(this);
		}
	}
}
