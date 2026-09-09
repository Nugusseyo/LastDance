using System.Collections.Generic;
using UnityEngine;

namespace Resources.DataBase.Upgrade_Data
{
	[ExcelAsset]
	public class UpgradeDB : ScriptableObject
	{
		public List<UpgradeData> UpgradeSheet;
	}
}
