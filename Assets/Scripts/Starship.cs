using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameManager;

public class Starship : MonoBehaviour
{
    public int playerNumber;
    public ShipAsset stats;
    public GameObject marker;
    public List<(Starship ship, int range)> targets = new List<(Starship, int)>();
    [HideInInspector] public bool repairing = false;
    [HideInInspector] public bool activated = false;
    [HideInInspector] public Vector2Int newArea;
    [SerializeField] TextMeshProUGUI shipText;
    [SerializeField] Slider HP;
    [SerializeField] Slider SP;
    private Vector2Int area;
    private int health;
    private int shields;

    public Vector3 Position { get => ((RectTransform)transform).anchoredPosition;
        set => ((RectTransform)transform).anchoredPosition = value; }
    public List<Starship> AreaShips => manager.position[area];
    public List<Starship> FriendlyShips => playerNumber == 1 ? manager.player1Ships : manager.player2Ships;
    public Vector2Int Area { get => area; private set {
        AreaShips.Remove(this);
        if (AreaShips.Count > 0) manager.AddFlag(area);
        manager.position[area = value].Add(this);
        manager.AddFlag(value);
    } }
    public int Health { get => (int)HP.value; private set => SetSlider(HP, value); }
    public int Shields { get => (int)SP.value; private set => SetSlider(SP, value); }
    public int Armor => Percent(stats.armor, HealthPercent);
    public int Repair => Percent(stats.repair, HealthPercent);
    public int ShieldRegen => Percent(stats.shieldRegeneration, HealthPercent);
    public int Evasion => Percent(stats.evasion, WeightedPercent);
    public int Speed => Percent(stats.speed, HealthPercent);
    public float HealthPercent => (float)Health / stats.health;
    public float WeightedPercent => HealthPercent * 0.7f + 0.3f;

    public void Init(int? number, int player, Vector2Int startPos, ShipAsset stats)
    {
        this.stats = stats;
        playerNumber = player;
        health = Health = (int)(HP.maxValue = stats.health);
        shields = Shields = (int)(SP.maxValue = stats.shields);
        name = stats.name.Replace("Stats", number.ToString() ?? "");
        shipText.text = stats.symbol + number ?? "";
        shipText.color = player == 1 ? Color.red : Color.blue;
        marker.SetActive(player == 1 && stats.isShip);
        manager.AddFlag(area = newArea = startPos);
        AreaShips.Add(this);
        FriendlyShips.Add(this);
        if (stats.isShip)
        {
            manager.numShips++;
            manager.ShipMovePhase += SetArea;
        }
        else
        {
            ((RectTransform)transform).rect.Set(Position.x, Position.y, 50, 40);
            if (Shields == 0) SP.gameObject.SetActive(false);
            manager.numSquads++;
            manager.SquadMovePhase += SetArea;
        }
        manager.ContinuePhase += EndOfPhase;
        for (int i = 0; i < stats.armament.Count; i++) { targets.Add((null, 0)); }
        gameObject.SetActive(true);
    }

    public void OnClick()
    {
        if (Phase - playerNumber == (stats.isShip ? -1 : 2)) { activated = true; marker.SetActive(false); manager.ToggleMove(this); }
        else if (Phase - playerNumber == 5) { activated = true; marker.SetActive(false); manager.ToggleWeapons(this); }
        else if (Phase + playerNumber == 8) { manager.AssignTarget(this); }
    }

    public void RecieveFire(WeaponAsset weapon, int range, float percent)
    {
        int damage = manager.CalculateDamage(Percent(weapon.accuracy, percent), Evasion, Percent(weapon.damage, percent),
            weapon.range, range);
        if (shields == 0) { LoseHealth(damage); }
        else if (shields >= damage) { shields -= damage; }
        else { LoseHealth(damage - shields); shields = 0; }
    }

    public void DisplayStats(bool display)
    {
        if (display) manager.DisplayStats(this);
        else manager.HideStats();
    }

    private void LoseHealth(int damage) => health -= Mathf.Max(0, damage - Armor);

    private void SetArea()
    {
        if (activated) { Area = newArea; activated = false; }
    }

    private void EndOfPhase()
    {
        if (Phase == 8) Fire();
        else if (Phase == 9) End();
        else if (Phase == 10) EndOfRound();
    }

    private void Fire()
    {
        if (activated)
        {
            for (int i = 0; i < stats.armament.Count; i++)
            {
                if (targets[i].ship != null)
                {
                    if (!repairing) targets[i].ship.RecieveFire(stats.armament[i], targets[i].range, WeightedPercent);
                    targets[i] = (null, 0);
                }
            }
        }
    }

    private void End()
    {
        if (activated && repairing)
        {
            int endHealth = health + Repair;
            health = Mathf.Min(endHealth, stats.health);
            if (endHealth > stats.health) { shields += endHealth - stats.health; }
            repairing = false;
        }
        activated = false;
        Health = health; Shields = shields;
    }

    private void EndOfRound()
    {
        if (health <= 0)
        {
            FriendlyShips.Remove(this);
            AreaShips.Remove(this);
            manager.AddFlag(area);
            if (stats.isShip) manager.ShipMovePhase -= SetArea;
            else manager.SquadMovePhase -= SetArea;
            manager.ContinuePhase -= EndOfPhase;
            if (FriendlyShips.Count == 1) { print($"Player {playerNumber % 2} won the game!"); }
            Destroy(gameObject);
            return;
        }
        shields = Shields += ShieldRegen;
    }

    private static void SetSlider(Slider slider, int value)
    {
        slider.value = value;
        if (value <= 0) slider.fillRect.gameObject.SetActive(false);
        else if (!slider.fillRect.gameObject.activeSelf) slider.fillRect.gameObject.SetActive(true);
    }

    public static int Percent(int number, float percent) => Mathf.RoundToInt(number * percent);
}