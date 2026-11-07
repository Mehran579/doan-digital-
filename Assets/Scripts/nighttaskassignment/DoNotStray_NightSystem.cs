// Do Not Stray: night task assignment logic (pure C#, no Unity dependency).
// Drop into Unity (C# 7+) or any .NET project. Port to GDScript by copying the formulas.
using System;
using System.Collections.Generic;
using System.Linq;

namespace DoNotStray
{
    public enum Resource { Firewood, Water, Food }
    public enum Status { Ready, Abducted, Dead }

    // ---------- every tunable number lives here ----------
    public class Config
    {
        // Daily luck
        public double LuckSigma = 20;          // how much luck swings day to day
        public double LuckMin = 5, LuckMax = 95;
        public double HurtLuckPenalty = 8;
        public double ShortageLuckPenalty = 10; // per resource you ran short of last night

        // Evening gathering: chance a person is taken
        public double BaseDanger = 0.20;       // set per level (0.08 easy .. 0.25 hard)
        public double NightGrowth = 0.25;      // +25% danger per night spent in this level
        public double NightCap = 2.5;
        public double EscapeWeight = 0.65;     // safety = 65% escape (fixed) + 35% luck (daily)
        public double SafetySwing = 1.8;       // how strongly safety changes risk (safety 0 => x1.9, 100 => x0.1)
        public double PartnerShield = 0.6;     // a partner with great escape cuts your risk by up to 60%
        public double SizeFactor2 = 0.45;      // pair
        public double SizeFactor3Plus = 0.30;  // group of 3+
        public double MinChance = 0.02, MaxChance = 0.60;

        // Finding items
        public double FindLuckSwing = 0;       // finding is skill-based. Raise above 0 to let luck matter again
        public double TeamworkBonus = 0.08;    // +8% find chance per extra party member

        // Night (everyone asleep): random abduction
        public double NightBase = 0.05, NightStep = 0.05, NightMax = 0.50;
        public double FireOnMult = 0.5;        // enough firewood: half the risk
        public double FireOffMult = 1.5;       // not enough firewood: +50% risk

        // Consumption per person per night (leader counts as a person)
        public Dictionary<Resource, double> NeedPerPerson = new Dictionary<Resource, double>
            { { Resource.Firewood, 0.5 }, { Resource.Water, 1 }, { Resource.Food, 1 } };
    }

    public class Survivor
    {
        public string Name;
        public double BaseLuck;                // 0-100, personality, never changes
        public double Escape;                  // 0-100, fixed: how well they get away from the creature
        public double TodayLuck;               // rolled every morning
        public int Capacity;                   // max items they can carry
        public Dictionary<Resource, double> FindBase = new Dictionary<Resource, double>(); // per-slot chance
        public bool Hurt;
        public Status Status = Status.Ready;
        public double FindBaseFor(Resource r) { double v; return FindBase.TryGetValue(r, out v) ? v : 0.5; }
    }

    public class Party { public Resource Task; public List<Survivor> Members = new List<Survivor>(); }

    public static class Formulas
    {
        public static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }

        // Safety mixes fixed escape ability with today's luck. Higher safety = lower multiplier.
        public static double Vulnerability(Survivor s, Config c)
        {
            double safety = c.EscapeWeight * s.Escape + (1 - c.EscapeWeight) * s.TodayLuck;
            return 1 + c.SafetySwing * (0.5 - safety / 100.0);
        }

        public static double Gaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        // Run every morning for everyone still Ready
        public static void RollDailyLuck(IEnumerable<Survivor> team, Config c, Random rng, int shortagesLastNight)
        {
            foreach (var s in team.Where(x => x.Status == Status.Ready))
            {
                double l = s.BaseLuck + Gaussian(rng) * c.LuckSigma;
                if (s.Hurt) l -= c.HurtLuckPenalty;
                l -= shortagesLastNight * c.ShortageLuckPenalty;
                s.TodayLuck = Clamp(l, c.LuckMin, c.LuckMax);
            }
        }

        // Chance this person is taken while gathering
        public static double KidnapChance(Survivor s, Party p, int nightsInLevel, Config c)
        {
            int size = p.Members.Count;
            double sizeF = size <= 1 ? 1 : size == 2 ? c.SizeFactor2 : c.SizeFactor3Plus;
            double shield = 1;
            if (size > 1)
            {
                double best = p.Members.Where(m => m != s).Max(m => m.Escape);
                shield = 1 - c.PartnerShield * best / 100.0;
            }
            double night = Math.Min(c.NightCap, 1 + c.NightGrowth * nightsInLevel);
            return Clamp(c.BaseDanger * night * Vulnerability(s, c) * sizeF * shield, c.MinChance, c.MaxChance);
        }

        // Chance each carry slot comes back filled
        public static double FindChance(Survivor s, Party p, Config c)
        {
            double luckF = 1 + c.FindLuckSwing * (2 * s.TodayLuck / 100.0 - 1);
            double team = 1 + c.TeamworkBonus * (p.Members.Count - 1);
            return Clamp(s.FindBaseFor(p.Task) * luckF * team, 0.05, 0.95);
        }

        public static double NightAttackChance(int nightsInLevel, bool fireOn, Config c)
        {
            double p = (c.NightBase + c.NightStep * nightsInLevel) * (fireOn ? c.FireOnMult : c.FireOffMult);
            return Clamp(p, 0, c.NightMax);
        }
    }

    public class Inventory
    {
        public Dictionary<Resource, int> Stock = new Dictionary<Resource, int>();
        public Dictionary<Resource, int> Max = new Dictionary<Resource, int>();
        public Inventory(int maxEach)
        {
            foreach (Resource r in Enum.GetValues(typeof(Resource))) { Stock[r] = 0; Max[r] = maxEach; }
        }
        public int Add(Resource r, int n) { int added = Math.Min(n, Max[r] - Stock[r]); Stock[r] += added; return added; }
        public int Take(Resource r, int n) { int t = Math.Min(n, Stock[r]); Stock[r] -= t; return t; }
        public static int Need(Resource r, int peopleAlive, Config c) { return (int)Math.Ceiling(peopleAlive * c.NeedPerPerson[r]); }
    }

    public class GatherReport
    {
        public Dictionary<Resource, int> Gathered = new Dictionary<Resource, int>();
        public List<Survivor> Taken = new List<Survivor>();
    }

    public class Preview
    {
        public double ChanceAllSafe, AvgTaken;
        public Dictionary<Resource, double> AvgGathered = new Dictionary<Resource, double>();
        public Dictionary<Resource, double> ChanceMeetNeed = new Dictionary<Resource, double>();
    }

    public class NightResult
    {
        public int Shortages;           // how many resources ran short (feeds tomorrow's luck)
        public bool Attacked;
        public Survivor Victim;
    }

    public static class Evening
    {
        // Pure: rolls the whole evening, changes nothing. Call Apply() to commit.
        public static GatherReport Resolve(List<Party> parties, int nightsInLevel, Config c, Random rng)
        {
            var rep = new GatherReport();
            foreach (Resource r in Enum.GetValues(typeof(Resource))) rep.Gathered[r] = 0;
            foreach (var p in parties)
                foreach (var s in p.Members)
                {
                    if (rng.NextDouble() < Formulas.KidnapChance(s, p, nightsInLevel, c)) { rep.Taken.Add(s); continue; }
                    // Predictable haul: the expected amount, only the fraction is rolled
                    double expected = s.Capacity * Formulas.FindChance(s, p, c);
                    double whole = Math.Floor(expected);
                    int haul = (int)whole + (rng.NextDouble() < expected - whole ? 1 : 0);
                    rep.Gathered[p.Task] += Math.Min(haul, s.Capacity);
                }
            return rep;
        }

        public static void Apply(GatherReport rep, Inventory inv)
        {
            foreach (var kv in rep.Gathered) inv.Add(kv.Key, kv.Value);
            foreach (var s in rep.Taken) s.Status = Status.Abducted;
        }

        // For the live "this plan is X% safe" readout in the UI
        public static Preview Evaluate(List<Party> parties, Inventory inv, int peopleAlive, int nightsInLevel,
                                       Config c, int runs = 1000, int seed = 1)
        {
            var pv = new Preview { ChanceAllSafe = 1 };
            foreach (var p in parties)
                foreach (var s in p.Members)
                    pv.ChanceAllSafe *= 1 - Formulas.KidnapChance(s, p, nightsInLevel, c);

            var rng = new Random(seed);
            foreach (Resource r in Enum.GetValues(typeof(Resource))) { pv.AvgGathered[r] = 0; pv.ChanceMeetNeed[r] = 0; }
            for (int i = 0; i < runs; i++)
            {
                var rep = Resolve(parties, nightsInLevel, c, rng);
                pv.AvgTaken += rep.Taken.Count;
                foreach (Resource r in Enum.GetValues(typeof(Resource)))
                {
                    pv.AvgGathered[r] += rep.Gathered[r];
                    int have = Math.Min(inv.Max[r], inv.Stock[r] + rep.Gathered[r]);
                    if (have >= Inventory.Need(r, peopleAlive, c)) pv.ChanceMeetNeed[r]++;
                }
            }
            pv.AvgTaken /= runs;
            foreach (Resource r in Enum.GetValues(typeof(Resource))) { pv.AvgGathered[r] /= runs; pv.ChanceMeetNeed[r] /= runs; }
            return pv;
        }
    }

    public static class Night
    {
        // Sleep phase: consume supplies, then maybe someone is taken. Show it next morning.
        public static NightResult Run(Inventory inv, List<Survivor> team, int peopleAlive,
                                      int nightsInLevel, Config c, Random rng)
        {
            var res = new NightResult();
            bool fireOn = inv.Stock[Resource.Firewood] >= Inventory.Need(Resource.Firewood, peopleAlive, c);
            foreach (Resource r in Enum.GetValues(typeof(Resource)))
            {
                int need = Inventory.Need(r, peopleAlive, c);
                if (inv.Take(r, need) < need) res.Shortages++;
            }
            var pool = team.Where(s => s.Status == Status.Ready).ToList(); // leader is never picked
            if (pool.Count > 0 && rng.NextDouble() < Formulas.NightAttackChance(nightsInLevel, fireOn, c))
            {
                var w = pool.Select(s => Formulas.Vulnerability(s, c)).ToList();
                double roll = rng.NextDouble() * w.Sum(), acc = 0;
                int idx = 0;
                for (; idx < pool.Count - 1; idx++) { acc += w[idx]; if (roll < acc) break; }
                res.Attacked = true; res.Victim = pool[idx];
                res.Victim.Status = Status.Abducted;
            }
            return res;
        }
    }

    // Example: one full evening. Delete or adapt.
    public static class Demo
    {
        public static void Run()
        {
            var c = new Config(); var rng = new Random(7);
            Func<string, double, int, Survivor> mk = (n, luck, cap) => new Survivor
            {
                Name = n,
                BaseLuck = luck,
                Capacity = cap,
                FindBase = { { Resource.Firewood, 0.5 }, { Resource.Water, 0.5 }, { Resource.Food, 0.5 } }
            };
            var team = new List<Survivor> { mk("Nurse", 50, 3), mk("Botanist", 30, 3), mk("Detective", 60, 4),
                                            mk("Scout", 60, 3), mk("Strong guy", 40, 6) };
            var inv = new Inventory(12);
            int alive = team.Count + 1;                       // + leader
            Formulas.RollDailyLuck(team, c, rng, 0);
            var parties = new List<Party> {
                new Party { Task = Resource.Firewood, Members = { team[4], team[1] } },
                new Party { Task = Resource.Water, Members = { team[0], team[2] } },
                new Party { Task = Resource.Food, Members = { team[3] } } };
            var pv = Evening.Evaluate(parties, inv, alive, 1, c);
            Console.WriteLine("All safe: " + pv.ChanceAllSafe.ToString("P0") + ", avg taken: " + pv.AvgTaken.ToString("0.00"));
            var rep = Evening.Resolve(parties, 1, c, rng); Evening.Apply(rep, inv);
            var night = Night.Run(inv, team, alive, 1, c, rng);
            Console.WriteLine("Taken on trip: " + rep.Taken.Count + ", night victim: " + (night.Victim != null ? night.Victim.Name : "none"));
        }
    }
}