using System.Collections.Generic;
using UnityEngine;

namespace Resources.DataBase.Review_Data
{
	[ExcelAsset]
	public class ReviewDB : ScriptableObject
	{
		public List<ReviewData> ReviewSheet; // Replace 'EntityType' to an actual type that is serializable.
		public List<ReviewName> NameSheet; // Replace 'EntityType' to an actual type that is serializable.
	}
}
