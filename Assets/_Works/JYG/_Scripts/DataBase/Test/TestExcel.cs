using System.Collections.Generic;
using UnityEngine;

namespace _Works.JYG._Scripts.DataBase.Test
{
	[ExcelAsset]
	public class TestExcel : ScriptableObject
	{
		public List<TestCharacters> Characters; // Replace 'EntityType' to an actual type that is serializable.
		public List<TestEnums> Sheet2; // Replace 'EntityType' to an actual type that is serializable.
	}
}
