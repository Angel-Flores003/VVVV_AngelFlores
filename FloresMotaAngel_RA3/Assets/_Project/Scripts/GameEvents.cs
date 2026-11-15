using System;
using UnityEngine;

public static class GameEvents//Intermediari
{
	public static event Action<int> OnZoneChange;
	public static event Action<int> OnLoseLife;


    public static void ZoneChange(int zone) => OnZoneChange?.Invoke(zone);
	public static void LoseLife(int life) => OnLoseLife?.Invoke(life);
}