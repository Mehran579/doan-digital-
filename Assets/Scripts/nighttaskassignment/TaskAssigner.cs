// Do Not Stray: holds the evening assignments and runs the night. Works with NightUIManager.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DoNotStray;

public class TaskAssigner : MonoBehaviour
{
    [SerializeField] NightUIManager ui;

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

        ui.Init(cfg, inv, team);
        BeginEvening(night, level);
    }

    int PeopleAlive() { return team.Count(s => s.Status == Status.Ready) + 1; }   // + leader

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
        var report = Evening.Resolve(parties, nightsInLevel, cfg, rng);
        Evening.Apply(report, inv);
        var night = Night.Run(inv, team, PeopleAlive(), nightsInLevel, cfg, rng);
        lastShortages = night.Shortages;
        nightsInLevel++;
        // play your black fade here, then show the morning message:
        ui.ShowMorningReport(report, night);
        ui.RefreshInventory(PeopleAlive());     // stock now reflects the haul and what the night used
        ClearAssignments();
    }

    // Unity buttons can only pass numbers, so this takes the survivor's position in the team list
    public void ClickSurvivorByIndex(int i)
    {
        if (i >= 0 && i < team.Count) ClickSurvivor(team[i]);
    }

    // Hook to a "Next day" button: opens the next evening screen
    public void NextEvening() { night++; BeginEvening(night, level); }

    // Call when the player reaches the exit
    public void NextLevel() { nightsInLevel = 0; level++; }
}