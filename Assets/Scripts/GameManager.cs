using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager manager;

    public int mapSize = 6;
    public Dictionary<Vector2Int, List<Starship>> position = new Dictionary<Vector2Int, List<Starship>>();
    public Tilemap area;
    public Starship ship;
    public List<ShipAsset> p1;
    public List<ShipAsset> p2;
    public List<Starship> player1Ships = new List<Starship>();
    public List<Starship> player2Ships = new List<Starship>();
    [HideInInspector] public int numShips = 0;
    [HideInInspector] public int numSquads = 0;
    [SerializeField] Camera mainCamera;
    [SerializeField] Tile highlightTile;
    [SerializeField] Tile currentMoveTile;
    [SerializeField] GameObject shipCanvas;
    [SerializeField] InteractionElement statusPanel;
    [SerializeField] InteractionElement selectionPanel;
    [SerializeField] InteractionElement weaponPanel;
    [SerializeField] InteractionElement repairButton;
    [SerializeField] InteractionElement allWeaponsButton;
    [SerializeField] TextMeshProUGUI buttonText;
    [SerializeField] TextMeshProUGUI phaseText;
    [SerializeField] TextMeshProUGUI statText;
    [SerializeField] InteractionElement speedBar;
    [SerializeField] List<InteractionElement> weapons;
    private Starship selectedShip;
    private Starship displayedShip;
    private List<Vector2Int> areaFlags = new List<Vector2Int>();
    private readonly List<string> phaseNames = new List<string> { "P1 Move (Ships)", "P2 Move (Ships)", "Ship Move Phase",
        "P1 Move (Squadrons)", "P2 Move (Squadrons)", "Squad Move Phase", "P1 Fire", "P2 Fire", "Fire Phase", "End of round" };

    public Action ShipMovePhase = delegate { };
    public Action SquadMovePhase = delegate { };
    public Action ContinuePhase = delegate { };

    public static int Phase { get; private set; } = 0;

    void Awake()
    {
        manager = this;
        GetComponentInChildren<InteractionTrigger>().OnClick = SetMove;
        p1.Add(null);
        p2.Add(null);
        
        Vector2Int pos = new Vector2Int();
        for (pos.x = -mapSize; pos.x <= mapSize; pos.x++)
        {
            for (pos.y = -mapSize; pos.y <= mapSize; pos.y++)
            {
                if (area.HasTile((Vector3Int)pos)) position.Add(pos, new List<Starship>());
            }
        }

        string lastSymbol = "";
        int numClones = 0;
        pos = new Vector2Int(0, mapSize);
        for (int i = 0; i < p1.Count - 1; i++)
        {
            ShipAsset stats = p1[i];
            if (lastSymbol == stats.symbol)
            {
                numClones++;
                Instantiate(ship, shipCanvas.transform).Init(numClones, 1, pos, stats);
            }
            else
            {
                lastSymbol = stats.symbol;
                numClones = 1;
                Instantiate(ship, shipCanvas.transform).Init((p1[i + 1]?.symbol ?? "") == stats.symbol ? 1 : (int?)null, 1, pos, stats);
            }
            pos.x = (pos.x < 0 ? 0 : -1) - pos.x;
            if (pos.x == pos.y / 2 - (mapSize % 2 == 0 ? mapSize + 1 : -1)) pos = new Vector2Int(0, pos.y - 1);
        }
        lastSymbol = "";
        numClones = 0;
        pos = new Vector2Int(0, -mapSize);
        for (int i = 0; i < p2.Count - 1; i++)
        {
            ShipAsset stats = p2[i];
            if (lastSymbol == stats.symbol)
            {
                numClones++;
                Instantiate(ship, shipCanvas.transform).Init(numClones, 2, pos, stats);
            }
            else
            {
                lastSymbol = stats.symbol;
                numClones = 1;
                Instantiate(ship, shipCanvas.transform).Init((p2[i + 1]?.symbol ?? "") == stats.symbol ? 1 : (int?)null, 2, pos, stats);
            }
            pos.x = (pos.x < 0 ? 0 : -1) - pos.x;
            if (pos.x == -pos.y / 2 - (mapSize % 2 == 0 ? mapSize + 1 : -1)) pos = new Vector2Int(0, pos.y + 1);
        }
        UpdateMap();
    }

    void Update()
    {
        if (displayedShip != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { DisplayWeapon(1); }
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { DisplayWeapon(2); }
            else if (Input.GetKeyDown(KeyCode.Alpha3)) { DisplayWeapon(3); }
            else if (Input.GetKeyDown(KeyCode.Alpha4)) { DisplayWeapon(4); }
            else if (Input.GetKeyDown(KeyCode.Alpha5)) { DisplayWeapon(5); }
            else if (Input.GetKeyDown(KeyCode.Alpha6)) { DisplayWeapon(6); }
        }
        if (Input.GetAxis("Vertical") != 0)
        {
            float move = mainCamera.transform.position.y + Input.GetAxis("Vertical");
            if (Mathf.Abs(move) <= 15) mainCamera.transform.position = new Vector3(0, move, -1);
        }
    }

    public int CalculateDamage(int accuracy, int evasion, int damage, int range, int distance)
    {
        float precision = accuracy - (14 - range) * distance - evasion + Random.Range(-50, 50);
        return precision < 0 ? 0 : Mathf.RoundToInt((precision + 50) * 0.01f * damage);
    }

    public void FireAll()
    {
        if (selectedShip != null)
        {
            for (int i = 0; i < selectedShip.targets.Count; i++) { weapons[i].toggle.isOn = allWeaponsButton.toggle.isOn; }
        }
    }

    public void ToggleRepair()
    {
        bool repairing = repairButton.toggle.isOn;
        statusPanel.SetActive(repairing);
        selectedShip.repairing = repairing;
    }

    public void ToggleMove(Starship ship)
    {
        if (selectedShip != null)
        {
            area.SwapTile(highlightTile, null);
            area.SwapTile(currentMoveTile, null);
        }
        selectedShip = ship;
        selectionPanel.Text = ship.name + " - Move";
        if (ship.Speed == 0) { statusPanel.SetActive(true); }
        else
        {
            statusPanel.SetActive(false);
            speedBar.Text = $"Speed: {speedBar.slider.value = ship.Speed}";
            SelectTiles((Vector3Int)ship.Area, ship.Speed);
            area.SetTile((Vector3Int)ship.newArea + Vector3Int.forward, currentMoveTile);
        }
        selectionPanel.SetActive(true);
    }

    public void SetMove()
    {
        selectedShip.newArea = (Vector2Int)area.WorldToCell(mainCamera.ScreenToWorldPoint(Input.mousePosition));
        selectionPanel.SetActive(false);
        selectedShip = null;
        area.SwapTile(highlightTile, null);
        area.SwapTile(currentMoveTile, null);
    }

    public void SelectTiles(Vector3Int origin, int range)
    {
        int config = origin.y % 2 == 0 ? -1 : 1;
        Vector3Int pos; int span;
        for (int x = -range; x <= range; x++)
        {
            /* 1 => 0,1,1;  2 => 0,2,2,2,1;  3 => 0,2,3,3,3,3,1;  4 => 0,2,4,4,4,4,4,3,1;
            5 => 0,2,4,5,5,5,5,5,5,3,1;  6 => 0,2,4,6,6,6,6,6,6,6,5,3,1 */
            span = ((range + 1) / 2 > x + range) ? 2 * (x + range) : ((range / 2 > range - x) ? 1 - 2 * (x - range) : range);
            for (pos = origin + new Vector3Int(x * config, -span, 0); pos.y - origin.y <= span; pos.y++)
            {
                if (area.HasTile(pos)) area.SetTile(pos + Vector3Int.forward, highlightTile);
            }
        }
    }

    public void ToggleWeapons(Starship ship)
    {
        selectedShip = ship;
        repairButton.Text = $"Repair {ship.Repair}";
        repairButton.toggle.isOn = ship.repairing;
        allWeaponsButton.Text = "All weapons";
        allWeaponsButton.toggle.isOn = false;
        selectionPanel.Text = ship.name + " - Fire";
        statusPanel.SetActive(ship.repairing);
        selectionPanel.SetActive(true);
        if (weapons[0].gameObject.activeSelf)
        {
            foreach (InteractionElement toggle in weapons) { toggle.SetActive(false); }
        }
        for (int i = 0; i < ship.targets.Count; i++)
        {
            weapons[i].Text = $"{ship.stats.armament[i].name}: " + (ship.targets[i].ship == null ? "No target" :
                $"{ship.targets[i].ship.name} R{ship.targets[i].range}");
            weapons[i].toggle.isOn = false;
            weapons[i].SetActive(true);
        }
    }

    public bool AssignTarget(Starship target)
    {
        if (selectedShip != null)
        {
            bool inRange = false;
            int range = HexDistance(selectedShip.Area, target.Area);
            for (int i = 0; i < selectedShip.targets.Count; i++)
            {
                if (weapons[i].toggle.isOn && selectedShip.stats.armament[i].range >= range)
                    { selectedShip.targets[i] = (target, range); inRange = true; }
                weapons[i].SetActive(false);
            }
            if (selectionPanel.gameObject.activeSelf)
            {
                selectedShip = null;
                selectionPanel.SetActive(false);
                allWeaponsButton.Text = "All ships";
                allWeaponsButton.toggle.isOn = false;
            }
            return inRange;
        }
        else if (allWeaponsButton.toggle.isOn)
        {
            foreach (Starship ship in Phase == 6 ? player1Ships : player2Ships)
            {
                selectedShip = ship;
                FireAll();
                ship.marker.SetActive(!(ship.activated = AssignTarget(target)));
            }
            selectedShip = null;
            allWeaponsButton.toggle.isOn = false;
        }
        return false;
    }

    public void DisplayStats(Starship ship)
    {
        displayedShip = ship;
        ShipAsset stats = ship.stats;
        statText.text = $"{ship.name}\nHealth: {ship.Health}/{stats.health} ({Mathf.Round(ship.HealthPercent * 100)}%)\nShields: "
            + $"{ship.Shields}/{stats.shields}\nArmor: {ship.Armor}/{stats.armor}\nRepair: {ship.Repair}/{stats.repair}\nShield "
            + $"Regen: {ship.ShieldRegen}/{stats.shieldRegeneration}\nEvasion: {ship.Evasion}/{stats.evasion}\nSpeed: {ship.Speed}/"
            + $"{stats.speed}\nArmament:";
        for (int i = 0; i < stats.armament.Count; i++) { statText.text += $"\n  ({i + 1}) {stats.armament[i].name}"; }
        statText.gameObject.SetActive(true);
    }

    public void HideStats() { displayedShip = null; weaponPanel.SetActive(false); statText.gameObject.SetActive(false); }

    private void DisplayWeapon(int number)
    {
        List<WeaponAsset> armament = displayedShip.stats.armament;
        if (number <= armament.Count)
        {
            WeaponAsset weapon = armament[number - 1];
            float percent = displayedShip.WeightedPercent;
            weaponPanel.Text = $"Damage: {Starship.Percent(weapon.damage, percent)}/{weapon.damage}\nAccuracy: " +
                $"{Starship.Percent(weapon.accuracy, percent)}/{weapon.accuracy}\nRange: {weapon.range}";
            weaponPanel.secondaryPanel.anchoredPosition = new Vector2(0, (armament.Count - number) * 17 - 20);
            weaponPanel.SetActive(true);
        }
    }

    /*public void MaxMove()
    {
        if (Phase % 3 == 0)
        {
            foreach (Starship ship in player1Ships)
            {
                if (ship.stats.isShip && Phase == 0 || !ship.stats.isShip && Phase == 3) ship.newArea = ship.Area - ship.Speed;
            }
        }
        else if (Phase % 3 == 1)
        {
            foreach (Starship ship in player2Ships)
            {
                if (ship.stats.isShip && Phase == 1 || !ship.stats.isShip && Phase == 4) ship.newArea = ship.Area + ship.Speed;
            }
        }
    }*/

    private int HexDistance(Vector2Int origin, Vector2Int target)
    {
        Vector2Int vector = target - origin;
        int y = vector.y;
        int x = vector.x - y / 2;
        int z = -x - y;
        return Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z));
    }

    public void AddFlag(Vector2Int position)
    {
        if (!areaFlags.Contains(position)) areaFlags.Add(position);
    }

    private void UpdateMap()
    {
        List<Starship> areaShips; Vector3 worldPos;
        foreach (Vector2Int pos in areaFlags)
        {
            areaShips = position[pos];
            if (areaShips.Count == 0) continue;
            worldPos = area.CellToWorld((Vector3Int)pos);
            if (areaShips.Count == 1)
            {
                areaShips[0].Position = worldPos;
                areaShips[0].transform.localScale = Vector3.one * 0.03f;
            }
            else
            {
                int c = areaShips.Count;
                int n = c;
                foreach (Starship ship in areaShips)
                {
                    ship.Position = worldPos + new Vector3(n < 4 ? 0 : (n % 2 == 0 ? -5 : 5),
                        (n < 4 ? 6 : (n < 6 ? 4 : 5)) * (n - c >= c / 2 ? -1 : 1)) * 0.1f;
                    ship.transform.localScale = Vector3.one * (c == 2 ? 0.02f : 0.015f);
                    n++;
                }
                /* 2:  0,  6;   0, -6
                 * 3:  0,  6;  -5, -4;   5, -4
                 * 4: -5,  4;   5,  4;  -5, -5;   5, -5
                 */
            }
        }
        areaFlags.Clear();
    }

    public void Continue()
    {
        if (selectedShip != null)
        {
            if (Phase > 5)
            {
                allWeaponsButton.Text = "All ships";
                foreach (InteractionElement toggle in weapons) { toggle.SetActive(false); }
            }
            else
            {
                area.SwapTile(highlightTile, null);
                area.SwapTile(currentMoveTile, null);
            }
            selectionPanel.SetActive(false);
            selectedShip = null;
        }
        Phase++;
        switch (Phase)
        {
            case 2:
                ShipMovePhase();
                UpdateMap();
                Phase = numSquads > 0 ? 3 : 6;
                break;
            case 5:
                SquadMovePhase();
                UpdateMap();
                Phase++;
                break;
            case 8:
                allWeaponsButton.SetActive(false);
                ContinuePhase();
                Phase++;
                ContinuePhase();
                break;
            case 10:
                ContinuePhase();
                UpdateMap();
                Phase = numShips > 0 ? 0 : 3;
                statusPanel.Text = "ENGINES OFFLINE";
                repairButton.SetActive(false);
                speedBar.SetActive(true);
                break;
        }
        if (Phase == 6)
        {
            statusPanel.Text = "REPAIRING SHIP";
            speedBar.SetActive(false);
            repairButton.SetActive(true);
            allWeaponsButton.SetActive(true);
        }
        if (Phase % 3 == 0)
        {
            foreach (Starship ship in player1Ships) ship.marker.SetActive(Phase == 6 || Phase == (ship.stats.isShip ? 0 : 3));
            foreach (Starship ship in player2Ships) ship.marker.SetActive(false);
        }
        else if (Phase % 3 == 1 && Phase != 10)
        {
            foreach (Starship ship in player1Ships) ship.marker.SetActive(false);
            foreach (Starship ship in player2Ships) ship.marker.SetActive(Phase == 7 || Phase == (ship.stats.isShip ? 1 : 4));
        }
        /* P1 on at 0(sp), 3(sq), 6
         * P1 off at 1, 4, 7
         * P2 on at 1(sp), 4(sq), 7
         * P2 off at 3, 6, 9
         */
        if (Phase < 9)
        {

        }
        phaseText.text = phaseNames[Phase];
    }
}