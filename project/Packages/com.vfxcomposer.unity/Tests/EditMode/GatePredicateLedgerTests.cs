using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Fail-closed audit over docs/design/paradigm/GATE_PREDICATE_LEDGER.md: the 156 predicate ids of
    /// COMPILER_BOUNDARY_V2 §7.2 must every one be registered in the ledger with a status. An id that
    /// is not registered is an audit FAIL (the ledger is the explicit-exemption surface; silence is
    /// not an exemption). Referenced test names must really exist in this assembly so a renamed or
    /// deleted test cannot leave a stale "已实现" row.
    /// </summary>
    public sealed class GatePredicateLedgerTests
    {
        private const string LedgerPath = "docs/design/paradigm/GATE_PREDICATE_LEDGER.md";

        /// <summary>The full 156-id space (COMPILER_BOUNDARY_V2 §7.2 totals table).</summary>
        private static readonly (string Prefix, int Count)[] PredicateFamilies =
        {
            ("SG", 8), ("MG", 5), ("MV", 9), ("GL", 7), ("PR", 2),
            ("GP", 9),
            ("CP", 10),
            ("UI", 5),
            ("MS", 12),
            ("PH", 9),
            ("LT", 9),
            ("ST", 6), ("CT", 8), ("PX", 10),
            ("WL", 6), ("CM", 6), ("RT", 9), ("BD", 5), ("AU", 6),
            ("RF", 5),
            ("GA", 10)
        };

        private static string LedgerText()
        {
            var absolute = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "..", LedgerPath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.That(File.Exists(absolute), Is.True, LedgerPath + " must exist: unregistered predicate ids are an audit FAIL.");
            return File.ReadAllText(absolute);
        }

        [Test]
        public void TheIdSpaceIsExactly156()
        {
            Assert.That(PredicateFamilies.Sum(family => family.Count), Is.EqualTo(156));
        }

        [Test]
        public void EveryPredicateIdIsRegisteredInTheLedger()
        {
            var text = LedgerText();
            var registered = RegisteredIds(text);
            var missing = new List<string>();
            foreach (var (prefix, count) in PredicateFamilies)
            {
                for (var index = 1; index <= count; index++)
                {
                    var id = prefix + "-" + index;
                    if (!registered.Contains(id)) missing.Add(id);
                }
            }

            Assert.That(missing, Is.Empty,
                "Unregistered predicate id(s) — the ledger fails closed:\n" + string.Join(", ", missing));
        }

        [Test]
        public void TheLedgerRegistersNoIdOutsideTheClosedSpace()
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (prefix, count) in PredicateFamilies)
                for (var index = 1; index <= count; index++)
                    known.Add(prefix + "-" + index);

            var ghosts = RegisteredIds(LedgerText()).Where(id => !known.Contains(id)).ToArray();
            Assert.That(ghosts, Is.Empty,
                "The ledger registers ghost predicate id(s) outside COMPILER_BOUNDARY_V2 §7.2:\n" + string.Join(", ", ghosts));
        }

        [Test]
        public void TheSummaryCountsEqualThePerRowStatusCounts()
        {
            var text = LedgerText();

            // Per-row counts: the status cell is the last cell of a predicate row and must be one of
            // the three enum values; range rows (e.g. "GP-2 ~ GP-9") count by their expanded id span.
            var perStatus = new Dictionary<string, int> { ["已实现"] = 0, ["T3待做"] = 0, ["条件豁免"] = 0 };
            foreach (Match match in Regex.Matches(text, @"^\|\s*([A-Z]{2})-(\d+)(?:\s*~\s*(?:[A-Z]{2}-)?(\d+))?\s*\|(.*)\|\s*$", RegexOptions.Multiline))
            {
                var cells = match.Groups[4].Value.Split('|');
                var status = cells[cells.Length - 1].Trim();
                if (!perStatus.ContainsKey(status)) continue; // §12/§13 tables carry non-status columns.
                var first = int.Parse(match.Groups[2].Value);
                var last = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : first;
                perStatus[status] += last - first + 1;
            }

            Assert.That(perStatus.Values.Sum(), Is.EqualTo(156), "Every predicate row must carry one of the three enum status values.");

            // Summary rows: "| <label> | <count> |" in §0. The label leads with the enum value.
            var summary = new Dictionary<string, int>();
            foreach (Match match in Regex.Matches(text, @"^\|\s*(已实现|T3待做|条件豁免)[^|]*\|\s*(\d+)\s*\|\s*$", RegexOptions.Multiline))
                summary[match.Groups[1].Value] = int.Parse(match.Groups[2].Value);

            foreach (var status in perStatus.Keys)
            {
                Assert.That(summary.ContainsKey(status), Is.True, "The §0 summary is missing the status row: " + status);
                Assert.That(summary[status], Is.EqualTo(perStatus[status]),
                    "§0 summary drifted from the per-row counts for '" + status + "' (summary " + summary[status] + " vs rows " + perStatus[status] + ").");
            }
        }

        [Test]
        public void EveryReferencedEditModeTestNameReallyExists()
        {
            var text = LedgerText();
            var assembly = typeof(GatePredicateLedgerTests).Assembly;
            var knownNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in assembly.GetTypes())
                foreach (var method in type.GetMethods())
                    knownNames.Add(type.Name + "." + method.Name);

            var stale = new List<string>();
            foreach (Match match in Regex.Matches(text, @"`([A-Za-z0-9]+Tests)\.([A-Za-z0-9_]+)`"))
            {
                var reference = match.Groups[1].Value + "." + match.Groups[2].Value;
                // PlayMode suites live in another assembly; only EditMode references are resolvable here.
                if (match.Groups[1].Value == "T2cParadigmSampleCaptureTests") continue;
                if (!knownNames.Contains(reference)) stale.Add(reference);
            }

            Assert.That(stale, Is.Empty,
                "The ledger references EditMode test name(s) that do not exist (stale 已实现 rows):\n" + string.Join("\n", stale));
        }

        private static HashSet<string> RegisteredIds(string text)
        {
            var registered = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(text, @"^\|\s*([A-Z]{2})-(\d+)(?:\s*~\s*(?:[A-Z]{2}-)?(\d+))?\s*\|", RegexOptions.Multiline))
            {
                var prefix = match.Groups[1].Value;
                var first = int.Parse(match.Groups[2].Value);
                var last = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : first;
                for (var index = first; index <= last; index++) registered.Add(prefix + "-" + index);
            }

            return registered;
        }
    }
}
