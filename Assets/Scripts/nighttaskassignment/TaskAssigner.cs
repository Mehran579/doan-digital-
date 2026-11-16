// Do Not Stray: holds the evening assignments and runs the night. Works with NightUIManager.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DoNotStray;

public class TaskAssigner : MonoBehaviour
{
    public static TaskAssigner Instance { get; private set; }

    class RescueRecord
    {
        public Survivor survivor;
        public GameObject original;
        public GameObject copy;
        public RescueHidingSpot spot;
        public int daysRemaining = 1;
    }

    [SerializeField] NightUIManager ui;
    [SerializeField] bool beginOnStart = true;

    Config cfg; Inventory inv; List<Survivor> team;
    // Always exactly 3 parties, one per task. Nobody travels anywhere, it is only UI and maths.
    readonly List<Party> parties = new List<Party>
    {
        new Party { Task = Resource.Firewood },
        new Party { Task = Resource.Water },
        new Party { Task = Resource.Food }
    };
    readonly System.Random rng = new System.Random();
    Survivor selected;
    Resource? activeTask;   // the task card the player clicked last
    int nightsInLevel, lastShortages;
    int night = 1, level = 1;

    public System.Action OnResumeExploration;
    public System.Action<Survivor> OnSurvivorRescued;
    readonly List<RescueRecord> rescues = new List<RescueRecord>();
    readonly List<Survivor> expiredRescues = new List<Survivor>();

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ConfigureForTimedLevel(NightUIManager manager, double levelBaseDanger)
    {
        ui = manager;
        beginOnStart = false;
        config.BaseDanger = levelBaseDanger;
    }

    public void StartTimedEvening() { BeginEvening(night, level); }

    [System.Serializable]
    public class SurvivorSetup
    {
        public string name;
        [Range(0, 100)] public float baseLuck = 50;
        [Range(0, 100)] public float escape = 40;
        public int capacity = 3;
        [Range(0, 1)] public float findFirewood = 0.5f;
        [Range(0, 1)] public float findWater = 0.5f;
        [Range(0, 1)] public float findFood = 0.5f;
    }

    static SurvivorSetup S(string n, float luck, float esc, int cap, float wood, float water, float food)
    {
        return new SurvivorSetup
        {
            name = n,
            baseLuck = luck,
            escape = esc,
            capacity = cap,
            findFirewood = wood,
            findWater = water,
            findFood = food
        };
    }

    static List<SurvivorSetup> DefaultRoster()
    {
        return new List<SurvivorSetup>
        {
            //  name           luck escape cap  wood  water food
            S("Nurse",         60,  25,   3,  0.50f, 0.50f, 0.55f),
            S("Botanist",      40,  20,   3,  0.60f, 0.75f, 0.90f),
            S("Detective",     50,  65,   4,  0.50f, 0.55f, 0.50f),
            S("Scout",         55,  45,   3,  0.85f, 0.90f, 0.80f),
            S("Strong guy",    40,  85,   6,  0.40f, 0.30f, 0.30f),
            S("Tutorial guy",  45,  30,   3,  0.40f, 0.40f, 0.40f)
        };
    }

    [Header("Rules (editable live in the Inspector)")]
    [SerializeField] Config config = new Config();

    [Header("Survivors (order = button index for ClickSurvivorByIndex)")]
    [SerializeField] List<SurvivorSetup> roster = DefaultRoster();

    // Right-click the component header and pick this if the roster is empty or you want the defaults back
    [ContextMenu("Reset roster to defaults")]
    void ResetRoster() { roster = DefaultRoster(); }

    // Builds the starting data from the Inspector values and opens the first evening
    void Start()
    {
        cfg = config;

        inv = new Inventory(cfg.MaxStorage);
        inv.Add(Resource.Firewood, cfg.StartFirewood);
        inv.Add(Resource.Water, cfg.StartWater);
        inv.Add(Resource.Food, cfg.StartFood);

        team = roster.Select(r => new Survivor
        {
            Name = r.name,
            BaseLuck = r.baseLuck,
            Escape = r.escape,
            Capacity = r.capacity,
            FindBase = { { Resource.Firewood, r.findFirewood }, { Resource.Water, r.findWater }, { Resource.Food, r.findFood } }
        }).ToList();

        if (ui == null)
        {
#if UNITY_2023_1_OR_NEWER
            ui = Object.FindFirstObjectByType<NightUIManager>(FindObjectsInactive.Include);
#else
            ui = Object.FindObjectOfType<NightUIManager>(true);
#endif
        }
        if (ui == null) { Debug.LogError("TaskAssigner needs the existing NightUIManager.", this); enabled = false; return; }
        ui.Init(cfg, inv, team);
        if (beginOnStart) BeginEvening(night, level);
    }

    int PeopleAlive() { return team.Count(s => s.Status == Status.Ready) + 1; }   // + leader

    public bool IsSurvivorReady(string survivorName)
    {
        // Before Start builds the roster, all scene survivors begin available.
        return team == null || team.Any(s => s.Name == survivorName && s.Status == Status.Ready);
    }

    // Evening starts: roll luck, open the screen
    public void BeginEvening(int night, int level)
    {
        Formulas.RollDailyLuck(team, cfg, rng, lastShortages);
        ClearAssignments();
        ui.ClearMorningReport();
        ui.SetHeader(night, level);
        ui.RefreshEvening(parties, PeopleAlive(), nightsInLevel);
    }

    // Hook to the three task cards: 0 = Firewood, 1 = Water, 2 = Food
    public void SelectTask(int task) { activeTask = (Resource)task; }

    // Hook to the survivor cards. With a task selected, clicking adds the survivor to it,
    // clicking again removes them. Without one, it just shows their details.
    public void ClickSurvivor(Survivor s)
    {
        selected = s;
        if (activeTask.HasValue && s.Status == Status.Ready)
        {
            if (parties.First(x => x.Task == activeTask.Value).Members.Contains(s))
            {
                Unassign(s);
                Redraw();
            }
            else Assign(s, activeTask.Value);   // moves them if they were on another task
        }
        else Redraw();
    }

    // Hook to the survivor buttons / cards (details only)
    public void Select(Survivor s)
    {
        selected = s;
        ui.ShowSurvivor(s, parties, nightsInLevel);
    }

    // Hook to the task buttons: 0 = Firewood, 1 = Water, 2 = Food (UnityEvents can't pass enums)
    public void AssignSelectedTo(int task)
    {
        if (selected == null || selected.Status != Status.Ready) return;
        Assign(selected, (Resource)task);
    }

    public void UnassignSelected()
    {
        if (selected == null) return;
        Unassign(selected);
        Redraw();
    }

    public void Assign(Survivor s, Resource task)
    {
        Unassign(s);
        parties.First(x => x.Task == task).Members.Add(s);
        Redraw();
    }

    void Unassign(Survivor s)
    {
        foreach (var p in parties) p.Members.Remove(s);
    }

    void ClearAssignments() { foreach (var p in parties) p.Members.Clear(); }

    void Redraw()
    {
        ui.RefreshEvening(parties, PeopleAlive(), nightsInLevel);
        if (selected != null) ui.ShowSurvivor(selected, parties, nightsInLevel);
    }

    // Hook to the "Start night" button
    public void StartNight()
    {
        ExpireRescueWindows();
        var report = Evening.Resolve(parties, nightsInLevel, cfg, rng);
        Evening.Apply(report, inv);
        var night = Night.Run(inv, team, PeopleAlive(), nightsInLevel, cfg, rng);
        Survivor tutorial = team.FirstOrDefault(s => s.Name == "Tutorial guy" && s.Status == Status.Ready);
        if (!night.Attacked && tutorial != null && (this.night >= 2 || rng.NextDouble() < 0.5))
        {
            tutorial.Status = Status.Abducted;
            night.Attacked = true;
            night.Victim = tutorial;
        }
        lastShortages = night.Shortages;
        nightsInLevel++;
        foreach (var survivor in report.Taken) MakeKidnappedCopy(survivor);
        if (night.Victim != null) MakeKidnappedCopy(night.Victim);
        RefreshRescueOutlines();
        // play your black fade here, then show the morning message:
        ui.ShowMorningReport(report, night);
        foreach (Survivor lost in expiredRescues)
            ui.AddMorningReportLine(lost.Name + " was lost after you could not find them in time.");
        ClearAssignments();
        if (selected != null && selected.Status != Status.Ready) selected = null;
        if (selected == null) ui.ClearSelectedSurvivor();
        ui.RefreshEvening(parties, PeopleAlive(), nightsInLevel);
        ui.RefreshInventory(PeopleAlive());     // stock now reflects the haul and what the night used
    }

    // Unity buttons can only pass numbers, so this takes the survivor's position in the team list
    public void ClickSurvivorByIndex(int i)
    {
        if (i >= 0 && i < team.Count) ClickSurvivor(team[i]);
    }

    // Hook to a "Next day" button: opens the next evening screen
    public void NextEvening()
    {
        night++;
        if (beginOnStart) BeginEvening(night, level);
        else OnResumeExploration?.Invoke();
    }

    // Call when the player reaches the exit
    public void NextLevel() { nightsInLevel = 0; level++; }

    void MakeKidnappedCopy(Survivor survivor)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        GameObject survivorsObject = GameObject.Find("survivors");
        Transform survivorsRoot = survivorsObject != null
            ? survivorsObject.transform
            : player != null ? player.transform : null;
        Transform original = null;
        if (survivorsRoot != null)
        {
            for (int i = 0; i < survivorsRoot.childCount; i++)
            {
                Transform child = survivorsRoot.GetChild(i);
                if (string.Equals(child.name, survivor.Name, System.StringComparison.OrdinalIgnoreCase))
                { original = child; break; }
            }
        }

        RescueHidingSpot spot = RescueHidingSpot.AllSpots.Count == 0
            ? null
            : RescueHidingSpot.AllSpots[Random.Range(0, RescueHidingSpot.AllSpots.Count)];
        Bounds bounds = default;
        if (spot == null && original != null)
        {
            var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
        }
        GameObject copy = null;
        if (original != null)
        {
            copy = Instantiate(original.gameObject, null);
            copy.name = survivor.Name + " (Kidnapped)";
            copy.transform.position = spot != null
                ? spot.transform.position
                : new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), original.position.z);
            foreach (var behaviour in copy.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var collider in copy.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in copy.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            original.gameObject.SetActive(false);
        }
        rescues.Add(new RescueRecord { survivor = survivor, original = original != null ? original.gameObject : null, copy = copy, spot = spot });
        if (copy != null) copy.AddComponent<KidnappedSurvivorOutline>();
        if (spot == null) Debug.LogWarning("No RescueHidingSpot exists; " + survivor.Name + " cannot be rescued in this scene.", this);
    }

    void ExpireRescueWindows()
    {
        expiredRescues.Clear();
        for (int i = rescues.Count - 1; i >= 0; i--)
        {
            RescueRecord record = rescues[i];
            record.daysRemaining--;
            if (record.daysRemaining > 0) continue;
            record.survivor.Status = Status.Dead;
            expiredRescues.Add(record.survivor);
            if (record.copy != null)
            {
                KidnappedSurvivorOutline outline = record.copy.GetComponent<KidnappedSurvivorOutline>();
                if (outline != null) outline.SetVisible(false);
            }
            if (record.copy != null) record.copy.name = record.survivor.Name + " (Lost)";
            rescues.RemoveAt(i);
        }
        RefreshRescueOutlines();
    }

    void RefreshRescueOutlines()
    {
        bool detectiveReady = team != null && team.Any(s => s.Name == "Detective" && s.Status == Status.Ready);
        foreach (RescueRecord record in rescues)
        {
            if (record.copy == null) continue;
            KidnappedSurvivorOutline outline = record.copy.GetComponent<KidnappedSurvivorOutline>();
            if (outline != null) outline.SetVisible(detectiveReady);
        }
    }

    public bool HasRescueAt(RescueHidingSpot spot)
    {
        return rescues.Any(r => r.spot == spot && r.daysRemaining > 0);
    }

    public bool CanDetectiveHint()
    {
        return team != null && team.Any(s => s.Name == "Detective" && s.Status == Status.Ready) &&
               rescues.Any(r => r.daysRemaining > 0 && r.copy != null && r.spot != null);
    }

    public bool TryGetDetectiveHint(out GameObject kidnappedCopy)
    {
        RescueRecord record = rescues.FirstOrDefault(r => r.daysRemaining > 0 && r.copy != null && r.spot != null);
        bool detectiveReady = team != null && team.Any(s => s.Name == "Detective" && s.Status == Status.Ready);
        kidnappedCopy = detectiveReady && record != null ? record.copy : null;
        return kidnappedCopy != null;
    }

    public bool TryRescueAt(RescueHidingSpot spot)
    {
        RescueRecord record = rescues.FirstOrDefault(r => r.spot == spot && r.daysRemaining > 0);
        if (record == null) return false;
        record.survivor.Status = Status.Ready;
        if (record.original != null) record.original.SetActive(true);
        if (record.copy != null) Destroy(record.copy);
        rescues.Remove(record);
        RefreshRescueOutlines();
        OnSurvivorRescued?.Invoke(record.survivor);
        Debug.Log(record.survivor.Name + " rescued at " + spot.SpotName + ".", this);
        return true;
    }
}
