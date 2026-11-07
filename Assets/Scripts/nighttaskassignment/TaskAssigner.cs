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

    // Builds the starting data and opens the first evening. Test values, change as you like.
    void Start()
    {
        cfg = new Config();

        inv = new Inventory(12);                // max 12 of each
        inv.Add(Resource.Firewood, 3);          // starting stock
        inv.Add(Resource.Water, 4);
        inv.Add(Resource.Food, 3);

        team = new List<Survivor>
        {
            //   name           luck escape cap  wood  water food
            Make("Nurse",         60,  25,   3,  0.50, 0.50, 0.55),
            Make("Botanist",      40,  20,   3,  0.60, 0.75, 0.90),
            Make("Detective",     50,  65,   4,  0.50, 0.55, 0.50),
            Make("Scout",         55,  45,   3,  0.85, 0.90, 0.80),
            Make("Strong guy",    40,  85,   6,  0.40, 0.30, 0.30),
            Make("Tutorial guy",  45,  30,   3,  0.40, 0.40, 0.40)
        };

        ui.Init(cfg, inv, team);
        BeginEvening(night, level);
        if (team.Count > 0) Select(team[0]);
    }

    static Survivor Make(string name, double baseLuck, double escape, int capacity,
                         double wood, double water, double food)
    {
        return new Survivor
        {
            Name = name,
            BaseLuck = baseLuck,
            Escape = escape,
            Capacity = capacity,
            FindBase = { { Resource.Firewood, wood }, { Resource.Water, water }, { Resource.Food, food } }
        };
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