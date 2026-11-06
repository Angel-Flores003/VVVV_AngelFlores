using System;
using UnityEngine;

public static class GameEvents
{
	public static event Action<int> OnZoneChange;

	public static void ZoneChange(int zone) => OnZoneChange?.Invoke(zone);
}