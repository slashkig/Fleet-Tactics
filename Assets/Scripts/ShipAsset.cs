using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Assets/Ship Stats")]
public class ShipAsset : ScriptableObject
{
    public int cost;
    public string symbol;
    public bool isShip;
    public int transport;
    public int health;
    public int armor;
    public int shields;
    public int repair;
    public int shieldRegeneration;
    public int speed;
    public int evasion;
    public List<WeaponAsset> armament;
}
