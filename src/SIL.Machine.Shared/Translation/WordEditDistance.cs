﻿namespace SIL.Machine.Translation
{
	internal class WordEditDistance : EditDistanceBase<string, char>
	{
		public double HitCost { get; set; }
		public double InsertionCost { get; set; }
		public double DeletionCost { get; set; }
		public double SubstitutionCost { get; set; }

		protected override int GetCount(string item)
		{
			return item.Length;
		}

		protected override char GetItem(string seq, int index)
		{
			return seq[index];
		}

		protected override double GetHitCost(char x, char y, bool isComplete)
		{
			return HitCost;
		}

		protected override double GetSubstitutionCost(char x, char y, bool isComplete)
		{
			return SubstitutionCost;
		}

		protected override double GetDeletionCost(char x)
		{
			return DeletionCost;
		}

		protected override double GetInsertionCost(char y)
		{
			return InsertionCost;
		}

		protected override bool IsHit(char x, char y, bool isComplete)
		{
			return x == y;
		}
	}
}
