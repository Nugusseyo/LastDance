using System.Collections.Generic;
using UnityEngine;

namespace Resources.DataBase.Human_Data
{
	[ExcelAsset]
	public class HumanDB : ScriptableObject
	{
		public List<HumanData> Sheet1; // Replace 'EntityType' to an actual type that is serializable.
	}
}
