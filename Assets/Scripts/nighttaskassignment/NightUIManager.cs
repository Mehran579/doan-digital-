// Do Not Stray: night assignment UI. Needs Unity + TextMeshPro. Put on one GameObject,
// drag your TMP text objects into the slots in the Inspector. Empty slots are skipped.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace DoNotStray
{
    public class NightUIManager : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] TMP_Text nightText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] TMP_Text campWarningText;

        [Header("Inventory (stock / max / need)")]
        [SerializeField] TMP_Text firewoodText;
        [SerializeField] TMP_Text waterText;
        [SerializeField] TMP_Text foodText;

        [Header("Selected survivor")]
        [SerializeField] TMP_Text selNameText;
        [SerializeField] TMP_Text selStatusText;
        [SerializeField] TMP_Text selLuckText;
        [SerializeField] TMP_Text selEscapeText;
        [SerializeField] TMP_Text selCapacityText;
        [SerializeField] TMP_Text selFindText;
        [SerializeField] TMP_Text selRiskText;

        [Header("Assignments")]
        [SerializeField] TMP_Text assignmentsText;

        [Header("Plan preview")]
        [SerializeField] TMP_Text planSafeText;
        [SerializeField] TMP_Text planTakenText;
        [SerializeField] TMP_Text planFirewoodText;
        [SerializeField] TMP_Text planWaterText;
        [SerializeField] TMP_Text planFoodText;

        [Header("Morning report")]
        [SerializeField] TMP_Text morningReportText;

        [Header("Options")]
        [SerializeField] bool showPercent = true;   // false = only Low / Medium / High / Critical

        Config cfg; Inventory inv; List<Survivor> team;

        // Call once when the level starts
        public void Init(Config config, Inventory inventory, List<Survivor> survivors)
        {
            cfg = config; inv = inventory; team = survivors;
        }

        // ---------- top bar ----------
        public void SetHeader(int night, int level)
        {
            Set(nightText, "Night " + night);
            Set(levelText, "Level " + level);
        }

        // ---------- inventory ----------
        public void RefreshInventory(int peopleAlive)
        {
            Set(firewoodText, InvLine("Firewood", Resource.Firewood, peopleAlive));
            Set(waterText, InvLine("Water", Resource.Water, peopleAlive));
            Set(foodText, InvLine("Food", Resource.Food, peopleAlive));
            bool fire = inv.Stock[Resource.Firewood] >= Inventory.Need(Resource.Firewood, peopleAlive, cfg);
            Set(campWarningText, fire ? "" : Color("Not enough firewood: attacks at night are more likely", "#ff8a5c"));
        }

        string InvLine(string label, Resource r, int peopleAlive)
        {
            int stock = inv.Stock[r], need = Inventory.Need(r, peopleAlive, cfg);
            string line = label + "  " + stock + " / " + inv.Max[r] + "   need " + need;
            return stock >= need ? line : Color(line, "#ff8a5c");
        }

        // ---------- survivor details (call when the player clicks a survivor) ----------
        public void ShowSurvivor(Survivor s, List<Party> parties, int nightsInLevel)
        {
            Set(selNameText, s.Name);
            Set(selStatusText, s.Status + (s.Hurt ? ", hurt" : ""));
            Set(selLuckText, "Luck today " + s.TodayLuck.ToString("0") + " (base " + s.BaseLuck.ToString("0") + ")");
            Set(selEscapeText, "Escape " + s.Escape.ToString("0"));
            Set(selCapacityText, "Carries up to " + s.Capacity + " items" + (selEscapeText == null ? ". Escape " + s.Escape.ToString("0") : ""));

            var find = new List<string>();
            foreach (Resource r in System.Enum.GetValues(typeof(Resource)))
            {
                var solo = new Party { Task = r, Members = { s } };
                find.Add(r + " " + (Formulas.FindChance(s, solo, cfg) * 100).ToString("0") + "%");
            }
            Set(selFindText, "Finds: " + string.Join(", ", find));

            // One risk number: their current assignment, or going alone if not assigned yet
            var current = parties.FirstOrDefault(p => p.Members.Contains(s))
                          ?? new Party { Members = { s } };
            Set(selRiskText, "Risk: " + Risk(Formulas.KidnapChance(s, current, nightsInLevel, cfg)));

        }

        // ---------- assignments list ----------
        public void RefreshAssignments(List<Party> parties, int nightsInLevel)
        {
            var sb = new StringBuilder();
            foreach (var p in parties)
            {
                var names = p.Members.Select(m => m.Name + " " + Risk(Formulas.KidnapChance(m, p, nightsInLevel, cfg)));
                sb.AppendLine(p.Task + ": " + (p.Members.Count == 0 ? "-" : string.Join(", ", names)));
            }
            var idle = team.Where(s => s.Status == Status.Ready && !parties.Any(p => p.Members.Contains(s))).Select(s => s.Name);
            sb.AppendLine("Stays at camp: " + (idle.Any() ? string.Join(", ", idle) : "nobody"));
            Set(assignmentsText, sb.ToString());
        }

        // ---------- plan preview (runs 1,000 simulated evenings, call when assignments change) ----------
        public void RefreshPlan(List<Party> parties, int peopleAlive, int nightsInLevel)
        {
            var pv = Evening.Evaluate(parties, inv, peopleAlive, nightsInLevel, cfg);
            Set(planSafeText, "Everyone safe: " + Good(pv.ChanceAllSafe));
            Set(planTakenText, "Expected people taken: " + pv.AvgTaken.ToString("0.0"));
            Set(planFirewoodText, "Enough firewood: " + Good(pv.ChanceMeetNeed[Resource.Firewood]));
            Set(planWaterText, "Enough water: " + Good(pv.ChanceMeetNeed[Resource.Water]));
            Set(planFoodText, "Enough food: " + Good(pv.ChanceMeetNeed[Resource.Food]));
        }

        // Convenience: refresh everything the evening screen shows
        public void RefreshEvening(List<Party> parties, int peopleAlive, int nightsInLevel)
        {
            RefreshInventory(peopleAlive);
            RefreshAssignments(parties, nightsInLevel);
            RefreshPlan(parties, peopleAlive, nightsInLevel);
        }

        // ---------- morning after the fade-out ----------
        public void ShowMorningReport(GatherReport evening, NightResult night)
        {
            var sb = new StringBuilder();
            foreach (var s in evening.Taken) sb.AppendLine(s.Name + " never came back from the trip.");
            if (night.Attacked) sb.AppendLine(night.Victim.Name + " was abducted in the night.");
            if (!evening.Taken.Any() && !night.Attacked) sb.AppendLine("The night passed quietly.");
            if (night.Shortages > 0) sb.AppendLine("You ran short of " + night.Shortages + " supply type(s). Everyone feels worse today.");
            Set(morningReportText, sb.ToString());
        }

        public void ClearMorningReport() { Set(morningReportText, ""); }

        // ---------- helpers ----------
        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
        static string Color(string s, string hex) { return "<color=" + hex + ">" + s + "</color>"; }

        // Risk shown as Low/Medium/High/Critical, optionally with the number
        string Risk(double p)
        {
            string band, hex;
            if (p < 0.08) { band = "Low"; hex = "#8fd16c"; }
            else if (p < 0.20) { band = "Medium"; hex = "#f2c94a"; }
            else if (p < 0.35) { band = "High"; hex = "#ff8a5c"; }
            else { band = "Critical"; hex = "#ff4d4d"; }
            return Color(showPercent ? band + " " + (p * 100).ToString("0") + "%" : band, hex);
        }

        // For "chance things go well": green when high
        static string Good(double p)
        {
            string hex = p >= 0.75 ? "#8fd16c" : p >= 0.4 ? "#f2c94a" : "#ff8a5c";
            return Color((p * 100).ToString("0") + "%", hex);
        }
    }
}