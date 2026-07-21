// GUI_SIMT_SRS_toxi_prediction_v29_fixed_uncorr_clustered.cs 
// ESAPI C# (.NET 4.5+) single-file script
// Layout update: table-style rows (no borders), centered cells, equal row heights; model name larger & italic bold with subscript;
// fixed model order: Ma,s; Ma,m; Mc,s; Mc,m. Cluster label (colored dot + (c#)) shown only for clustered rows (Mc,s and Mc,m).
// Only AFTER-correction results are displayed for unclustered models; clustered models now show UNCORRECTED (raw) results.
// Mini-plot axis range = 0.9×min .. 1.1×max of that model's predictions.
// Two images below table: 3D plot (normal) + bar plot (5× smaller). Window auto-sizes to content.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging; // for BitmapImage
using System.Windows.Documents;     // for Run / subscript
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

#pragma warning disable 0429

namespace VMS.TPS
{
    // ---------- Simple picker window to select which Exp* structures to include ----------
    internal sealed class SelectExpLesionsWindow : Window
    {
        private readonly List<Tuple<Structure, CheckBox>> _map = new List<Tuple<Structure, CheckBox>>();

        public SelectExpLesionsWindow(IEnumerable<Structure> expStructures)
        {
            Title = "Select Exp* lesions to include";
            Width = 420;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel { Margin = new Thickness(10) };

            var header = new TextBlock
            {
                Text = "Uncheck any Exp* structures you want to EXCLUDE from prediction:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.SemiBold
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            var btnAll = new Button { Content = "Select All", Margin = new Thickness(0, 0, 6, 0), MinWidth = 90 };
            var btnNone = new Button { Content = "Deselect All", Margin = new Thickness(0, 0, 6, 0), MinWidth = 90 };
            var btnContinue = new Button { Content = "Continue", Margin = new Thickness(6, 0, 6, 0), MinWidth = 90, IsDefault = true };
            var btnCancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };

            btnAll.Click += (s, e) => { foreach (var pair in _map) pair.Item2.IsChecked = true; };
            btnNone.Click += (s, e) => { foreach (var pair in _map) pair.Item2.IsChecked = false; };
            btnContinue.Click += (s, e) => { this.DialogResult = true; };

            buttonsPanel.Children.Add(btnAll);
            buttonsPanel.Children.Add(btnNone);
            buttonsPanel.Children.Add(new StackPanel { Width = 20 }); // spacer
            buttonsPanel.Children.Add(btnContinue);
            buttonsPanel.Children.Add(btnCancel);
            DockPanel.SetDock(buttonsPanel, Dock.Bottom);
            root.Children.Add(buttonsPanel);

            var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listHost = new StackPanel();
            scroller.Content = listHost;

            foreach (var st in expStructures.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
            {
                var cb = new CheckBox
                {
                    Content = st.Id,
                    IsChecked = true,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                _map.Add(Tuple.Create(st, cb));
                listHost.Children.Add(cb);
            }

            root.Children.Add(scroller);
            Content = root;
        }

        public List<Structure> GetIncludedLesions()
        {
            return _map.Where(p => p.Item2.IsChecked == true).Select(p => p.Item1).ToList();
        }
    }

    public class Script
    {
        // ---------- CONSTANTS FOR LAYOUT ----------
        private const double ModelRowHeight = 120;   // equal height for each model row
        private const double MiniPlotWidth = 160;
        private const double MiniPlotHeight = 100;
        private const double ModelFontDelta = 6;     // model name larger (+6pt relative to default)

        // ---------- PATHS ----------
        internal static class Paths
        {
            // Unclustered individual (3 separate JSONs)
            public const string JSON_V50 = @"C:\SRSDosePrediction\Models\v50_model.json";
            public const string JSON_V60 = @"C:\SRSDosePrediction\Models\v60_model.json";
            public const string JSON_V667 = @"C:\SRSDosePrediction\Models\v66_7_model.json";

            // Unclustered shared 3-in-1
            public const string SHARED_3IN1 = @"C:\SRSDosePrediction\Models\gbrt_model_3in1_detailed_with_unc.json";

            // Clustered (three-in-one per cluster) and clustered individual
            public const string C1_3IN1 = @"C:\SRSDosePrediction\Models\c1_model_3in1.json";
            public const string C2_3IN1 = @"C:\SRSDosePrediction\Models\c2_model_3in1.json";
            public const string C3_3IN1 = @"C:\SRSDosePrediction\Models\c3_model_3in1.json";
            public const string C1_INDIV = @"C:\SRSDosePrediction\Models\c1_model_indiv.json";
            public const string C2_INDIV = @"C:\SRSDosePrediction\Models\c2_model_indiv.json";
            public const string C3_INDIV = @"C:\SRSDosePrediction\Models\c3_model_indiv.json";

            // Two graphs (below the results list)
            public const string IMG_THREE_D_PLOT = @"C:\SRSDosePrediction\Images\THREE_D_PLOT.png"; // 1st (normal size)
            public const string IMG_THREE_D_BAR = @"C:\SRSDosePrediction\Images\THREE_D_BAR.png";  // 2nd (5× smaller)
        }

        // ---------- tolerant JSON helpers ----------
        internal static class J
        {
            public static int IndexAfterCI(string s, string[] keys)
            {
                if (s == null) return -1;
                for (int i = 0; i < keys.Length; i++)
                {
                    int k = CultureInfo.InvariantCulture.CompareInfo.IndexOf(s, keys[i], CompareOptions.IgnoreCase);
                    if (k >= 0)
                    {
                        int colon = s.IndexOf(':', k + keys[i].Length);
                        if (colon >= 0) return colon + 1;
                    }
                }
                return -1;
            }
            public static string ExtractBlock(string s, int startAtColon, char open, char close)
            {
                if (s == null || startAtColon < 0) return "";
                int pos = startAtColon;
                while (pos < s.Length && s[pos] != open) pos++;
                if (pos >= s.Length) return "";
                int depth = 0;
                for (int i = pos; i < s.Length; i++)
                {
                    char cc = s[i];
                    if (cc == open) depth++;
                    else if (cc == close)
                    {
                        depth--;
                        if (depth == 0) return s.Substring(pos, i - pos + 1);
                    }
                    else if (cc == '"')
                    {
                        i++;
                        while (i < s.Length)
                        {
                            if (s[i] == '"' && s[i - 1] != '\\') break;
                            i++;
                        }
                    }
                }
                return "";
            }
            public static string TrimOuter(string s, char open, char close)
            {
                if (string.IsNullOrEmpty(s)) return s;
                string t = s.Trim();
                if (t.Length >= 2 && t[0] == open && t[t.Length - 1] == close) return t.Substring(1, t.Length - 2);
                return t;
            }
            public static double ExtractDoubleCI(string s, string[] keys, double def)
            {
                int ai = IndexAfterCI(s, keys); if (ai < 0) return def;
                int i = ai;
                while (i < s.Length && !(char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+')) i++;
                if (i >= s.Length) return def;
                int j = i;
                while (j < s.Length)
                {
                    char cc = s[j];
                    if (char.IsDigit(cc) || cc == '-' || cc == '+' || cc == '.' || cc == 'E' || cc == 'e') j++;
                    else break;
                }
                double v;
                return double.TryParse(s.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
            }
            public static int ExtractIntCI(string s, string[] keys, int def) { return (int)Math.Round(ExtractDoubleCI(s, keys, def)); }
            public static bool ExtractBoolCI(string s, string[] keys, bool def)
            {
                int ai = IndexAfterCI(s, keys); if (ai < 0) return def;
                int j = ai;
                while (j < s.Length && (char.IsWhiteSpace(s[j]) || s[j] == ',')) j++;
                int start = j;
                while (j < s.Length && s[j] != ',' && s[j] != '}' && s[j] != ']') j++;
                string tok = s.Substring(start, Math.Max(0, j - start)).Trim().Trim('"');
                if (string.Compare(tok, "true", true, CultureInfo.InvariantCulture) == 0) return true;
                if (string.Compare(tok, "false", true, CultureInfo.InvariantCulture) == 0) return false;
                return def;
            }
            public static double[] ExtractDoubleArrayCI(string s, string[] keys)
            {
                int ai = IndexAfterCI(s, keys); if (ai < 0) return null;
                string block = ExtractBlock(s, ai, '[', ']');
                string inner = TrimOuter(block, '[', ']');
                if (string.IsNullOrWhiteSpace(inner)) return new double[0];
                List<double> vals = new List<double>(); int i = 0;
                while (i < inner.Length)
                {
                    while (i < inner.Length && (char.IsWhiteSpace(inner[i]) || inner[i] == ',')) i++;
                    if (i >= inner.Length) break;
                    int s0 = i;
                    while (i < inner.Length)
                    {
                        char cc = inner[i];
                        if (char.IsDigit(cc) || cc == '-' || cc == '+' || cc == '.' || cc == 'E' || cc == 'e') i++;
                        else break;
                    }
                    double v;
                    if (double.TryParse(inner.Substring(s0, i - s0), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) vals.Add(v);
                }
                return vals.ToArray();
            }
            public static List<string> ExtractStringArrayCI(string s, string[] keys)
            {
                int ai = IndexAfterCI(s, keys); if (ai < 0) return null;
                string block = ExtractBlock(s, ai, '[', ']'); string inner = TrimOuter(block, '[', ']');
                List<string> res = new List<string>(); int i = 0;
                while (i < inner.Length)
                {
                    while (i < inner.Length && (char.IsWhiteSpace(inner[i]) || inner[i] == ',')) i++;
                    if (i >= inner.Length) break;
                    if (inner[i] == '"')
                    {
                        int start = i + 1; int j = start; while (j < inner.Length) { if (j < inner.Length && inner[j] == '"' && inner[j - 1] != '\\') break; j++; }
                        if (j >= inner.Length) break; res.Add(inner.Substring(start, j - start)); i = j + 1;
                    }
                    else
                    {
                        int s0 = i; while (i < inner.Length && inner[i] != ',' && inner[i] != ']') i++;
                        string tok = inner.Substring(s0, i - s0).Trim().Trim('"'); if (tok.Length > 0) res.Add(tok);
                    }
                }
                return res;
            }
            public static string ExtractStringCI(string s, string[] keys)
            {
                int ai = IndexAfterCI(s, keys); if (ai < 0) return null;
                int j = ai;
                while (j < s.Length && (char.IsWhiteSpace(s[j]) || s[j] == ',')) j++;
                if (j >= s.Length) return null;
                if (j < s.Length && s[j] == '"')
                {
                    int start = j + 1; int i = start;
                    while (i < s.Length)
                    {
                        if (i < s.Length && s[i] == '"' && s[i - 1] != '\\') break;
                        i++;
                    }
                    if (i < s.Length) return s.Substring(start, i - start);
                }
                return null;
            }
        }

        // ---------- Patient feature helpers ----------
        private static double? TryGetRxGy(ScriptContext ctx)
        {
            try
            {
                if (ctx.PlanSetup != null && ctx.PlanSetup.TotalDose != null)
                {
                    double d = ctx.PlanSetup.TotalDose.Dose;
                    if (d >= 150.0) d /= 100.0;
                    return d;
                }
                if (ctx.ExternalPlanSetup != null && ctx.ExternalPlanSetup.TotalDose != null)
                {
                    double d = ctx.ExternalPlanSetup.TotalDose.Dose;
                    if (d >= 150.0) d /= 100.0;
                    return d;
                }
            }
            catch { }
            return null;
        }
        private static string FindPreferredReferenceId(StructureSet ss)
        {
            if (ss == null) return "";
            string[] prefs = { "BODY", "Outer Contour", "External", "Outline", "Patient" };
            foreach (var p in prefs)
            {
                var s = ss.Structures.FirstOrDefault(x => !x.IsEmpty && string.Equals(x.Id, p, StringComparison.OrdinalIgnoreCase));
                if (s != null) return s.Id;
            }
            var any = ss.Structures.FirstOrDefault(x => !x.IsEmpty);
            return any != null ? any.Id : "";
        }
        private static double GetStructureVolumeCc(StructureSet ss, string id)
        {
            try
            {
                var s = ss.Structures.FirstOrDefault(x => !x.IsEmpty && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                return (s != null) ? s.Volume : 0.0;
            }
            catch { return 0.0; }
        }

        // NEW: compute mesh surface area (mm^2); fallback handled by caller.
        private static double ComputeSurfaceAreaFromMesh(Structure s)
        {
            try
            {
                if (s == null || s.MeshGeometry == null || s.MeshGeometry.Positions == null || s.MeshGeometry.TriangleIndices == null)
                    return 0.0;

                var pts = s.MeshGeometry.Positions;
                var tris = s.MeshGeometry.TriangleIndices;
                if (pts == null || tris == null) return 0.0;

                double area = 0.0;
                int triCount = tris.Count / 3;
                for (int t = 0; t < triCount; t++)
                {
                    int i0 = tris[3 * t + 0];
                    int i1 = tris[3 * t + 1];
                    int i2 = tris[3 * t + 2];
                    var p0 = pts[i0]; var p1 = pts[i1]; var p2 = pts[i2];

                    double ux = p1.X - p0.X, uy = p1.Y - p0.Y, uz = p1.Z - p0.Z;
                    double vx = p2.X - p0.X, vy = p2.Y - p0.Y, vz = p2.Z - p0.Z;

                    double cx = uy * vz - uz * vy;
                    double cy = uz * vx - ux * vz;
                    double cz = ux * vy - uy * vx;

                    double triArea = 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
                    area += triArea;
                }
                return area; // mm^2
            }
            catch { return 0.0; }
        }

        // ---------- Cluster assigner (IDENTICAL to mini) ----------
        internal static class ClusterAssigner3D
        {
            // Mu/Sigma for log1p of [TotalVolume, BA_Ratio, NumTumors]
            private static readonly double[] Mu = new double[]
            {
                2.2093592502287134,  // TotalVolume
                1.0489649932719614,  // BA_Ratio
                1.8040058154549381   // NumTumors
            };
            private static readonly double[] Sigma = new double[]
            {
                1.1840434257719139,  // TotalVolume
                0.4702592224661849,  // BA_Ratio
                0.67526793310169209  // NumTumors
            };
            private static readonly double[,] Centroids = new double[,]
            {
                {  0.67105211248708863,  1.3340067715564432,  1.1763885360414785 },  // C1
                { -0.89890337510198681, -0.53219052210733342, -0.32251006569360435 },// C2
                {  0.81657046492991,    -0.43453417986928783, -0.62394425758313687 } // C3
            };

            // compute BA = AvgSurfaceArea / AvgEqSphereSA inside (same as mini).
            public static int AssignCluster(int noTumors, double totalVolCc, double avgSa_cm2, double avgEq_cm2)
            {
                double totalVol = Math.Max(0.0, totalVolCc);
                double baRatio = (avgEq_cm2 > 0.0) ? (avgSa_cm2 / avgEq_cm2) : 1.0;
                double numTum = Math.Max(0, noTumors);
                double[] raw = { totalVol, baRatio, numTum };

                double[] z = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    double li = Math.Log(1.0 + raw[i]);
                    z[i] = (li - Mu[i]) / Sigma[i];
                }

                double bestDist = double.MaxValue; int best = -1;
                for (int c = 0; c < 3; c++)
                {
                    double dx0 = z[0] - Centroids[c, 0];
                    double dx1 = z[1] - Centroids[c, 1];
                    double dx2 = z[2] - Centroids[c, 2];
                    double d = Math.Sqrt(dx0 * dx0 + dx1 * dx1 + dx2 * dx2);
                    if (d < bestDist) { bestDist = d; best = c; }
                }
                return best + 1; // 1..3
            }
        }

        /// ---------- Empirical %diff→volume correction (ONLY for Unclustered • Individual) ----------
        internal static class EmpiricalCorrection
        {
            private const double NO_CORR_THRESHOLD_CC = 150.0; // current threshold

            private struct Coeff { public double c, A, a; public Coeff(double _c, double _A, double _alpha) { c = _c; A = _A; a = _alpha; } }

            // y%(x) = c - A * exp( - (alpha^2) * x ),  x == CS volume (cc)
            private static readonly Coeff V50 = new Coeff(0.0, 38.2, 0.106);
            private static readonly Coeff V60 = new Coeff(0.863, 36.9, 0.125);
            private static readonly Coeff V667 = new Coeff(9.12, 37.7, 0.172);

            private static double PercentDiff(double x, Coeff k)
            {
                double a2 = k.a * k.a;
                return k.c - k.A * Math.Exp(-a2 * x);
            }
            private static double PercentDiffPrime(double x, Coeff k)
            {
                double a2 = k.a * k.a;
                return k.A * a2 * Math.Exp(-a2 * x); // % per cc
            }

            private static void Apply(ref double y_cs, ref double sigma, Coeff k)
            {
                if (double.IsNaN(y_cs)) return;

                if (y_cs > NO_CORR_THRESHOLD_CC) return;

                double p = PercentDiff(y_cs, k);             // %
                double q = 1.0 + p / 100.0;                  // unitless
                const double Q_MIN = 0.05; if (q < Q_MIN) q = Q_MIN;

                double y_ml = y_cs / q;

                double dp = PercentDiffPrime(y_cs, k) / 100.0;
                double dydy = (q - y_cs * dp) / (q * q);

                double s_in = (double.IsNaN(sigma) ? 0.0 : sigma);
                double s_ml = Math.Abs(dydy) * s_in;

                y_cs = y_ml;
                sigma = s_ml;
            }

            public static void CorrectV50(ref double y, ref double s) { Apply(ref y, ref s, V50); }
            public static void CorrectV60(ref double y, ref double s) { Apply(ref y, ref s, V60); }
            public static void CorrectV667(ref double y, ref double s) { Apply(ref y, ref s, V667); }
        }

        // ---------- Empirical %diff→volume correction (ONLY for Individual • Clustered) ----------
        internal static class ClusteredEmpiricalCorrection
        {
            private const double NO_CORR_THRESHOLD_CC = 150.0; // current threshold

            private struct Coeff { public double A, a, c; public Coeff(double _A, double _alpha, double _c) { A = _A; a = _alpha; c = _c; } }

            // y%(x) = A * exp( - (alpha^2) * x ) + c
            private static readonly Coeff V50 = new Coeff(8.81e3, 0.358, -47.7);
            private static readonly Coeff V60 = new Coeff(659.0, 0.279, -35.2);
            private static readonly Coeff V667 = new Coeff(7.44e3, 0.469, -41.4);

            private const double P_MIN = -90.0;
            private const double P_MAX = +60.0;

            private static double Pct(double x, Coeff k)
            {
                double a2 = k.a * k.a;
                return k.A * Math.Exp(-a2 * x) + k.c;
            }
            private static double PctPrime(double x, Coeff k)
            {
                double a2 = k.a * k.a;
                return -k.A * a2 * Math.Exp(-a2 * x); // % per cc
            }

            private static void Apply(ref double y_cs, ref double sigma, Coeff k)
            {
                if (double.IsNaN(y_cs)) return;

                if (y_cs > NO_CORR_THRESHOLD_CC) return;

                double x = y_cs;
                double p = Pct(x, k);
                double pCl = Math.Max(P_MIN, Math.Min(P_MAX, p));

                double q = 1.0 + pCl / 100.0;
                const double Q_MIN = 0.05, Q_MAX = 20.0;
                if (q < Q_MIN) q = Q_MIN; else if (q > Q_MAX) q = Q_MAX;

                double y_ml = y_cs / q;

                double dp = PctPrime(x, k) / 100.0;
                double dydy = (q - y_cs * dp) / (q * q);

                double s_in = (double.IsNaN(sigma) ? 0.0 : sigma);
                double s_ml = Math.Abs(dydy) * s_in;

                y_cs = y_ml;
                sigma = s_ml;
            }

            public static void CorrectV50(ref double y, ref double s) { Apply(ref y, ref s, V50); }
            public static void CorrectV60(ref double y, ref double s) { Apply(ref y, ref s, V60); }
            public static void CorrectV667(ref double y, ref double s) { Apply(ref y, ref s, V667); }
        }

        /// Empirical linear %diff correction for Unclustered • 3-in-1 (convert CS → ML)
        internal static class U31LinearCorrection
        {
            private const double NO_CORR_THRESHOLD_CC = 150.0; // current threshold

            private struct Lin { public double m, b; public Lin(double _m, double _b) { m = _m; b = _b; } }
            private static readonly Lin V50 = new Lin(0.0658404, -17.0418);
            private static readonly Lin V60 = new Lin(0.0173278, -11.0886);
            private static readonly Lin V667 = new Lin(0.104953, -16.9519);

            private const double P_MIN = -90.0;
            private const double P_MAX = +90.0;

            private static void Apply(ref double y_cs, ref double sigma, Lin k)
            {
                if (double.IsNaN(y_cs)) return;

                if (y_cs > NO_CORR_THRESHOLD_CC) return;

                double p = k.m * y_cs + k.b;
                if (p < P_MIN) p = P_MIN; else if (p > P_MAX) p = P_MAX;

                double q = 1.0 + p / 100.0;
                const double Q_MIN = 0.05, Q_MAX = 20.0;
                if (q < Q_MIN) q = Q_MIN; else if (q > Q_MAX) q = Q_MAX;

                double y_ml = y_cs / q;

                double dydy = (q - y_cs * (k.m / 100.0)) / (q * q);

                double s_in = (double.IsNaN(sigma) ? 0.0 : sigma);
                double s_ml = Math.Abs(dydy) * s_in;

                y_cs = y_ml;
                sigma = s_ml;
            }

            public static void CorrectV50(ref double y, ref double s) { Apply(ref y, ref s, V50); }
            public static void CorrectV60(ref double y, ref double s) { Apply(ref y, ref s, V60); }
            public static void CorrectV667(ref double y, ref double s) { Apply(ref y, ref s, V667); }
        }

        /// Empirical %diff correction for 3-in-1 • Clustered (convert CS → ML)
        internal static class Clustered3in1EmpiricalCorrection
        {
            private const double NO_CORR_THRESHOLD_CC = 150.0; // current threshold

            private struct Coeff { public double A, a, c; public Coeff(double _A, double _alpha, double _c) { A = _A; a = _alpha; c = _c; } }

            // y%(x) = A * exp( - (alpha^2) * x ) + c
            private static readonly Coeff V50 = new Coeff(647.0, 0.23, -45.0);
            private static readonly Coeff V60 = new Coeff(549.0, 0.25, -45.7);
            private static readonly Coeff V667 = new Coeff(1360.0, 0.367, -36.2);

            private const double P_MIN = -90.0;
            private const double P_MAX = +300.0;

            private static double Pct(double x, Coeff k)
            {
                double a2 = k.a * k.a;
                return k.A * Math.Exp(-a2 * x) + k.c;
            }
            private static double PctPrime(double x, Coeff k)
            {
                double a2 = k.a * k.a;
                return -k.A * a2 * Math.Exp(-a2 * x); // % per cc
            }

            private static void Apply(ref double y_cs, ref double sigma, Coeff k)
            {
                if (double.IsNaN(y_cs)) return;

                if (y_cs > NO_CORR_THRESHOLD_CC) return;

                double x = y_cs;
                double p = Pct(x, k);
                if (p < P_MIN) p = P_MIN; else if (p > P_MAX) p = P_MAX;

                double q = 1.0 + p / 100.0;
                const double Q_MIN = 0.05, Q_MAX = 20.0;
                if (q < Q_MIN) q = Q_MIN; else if (q > Q_MAX) q = Q_MAX;

                double y_ml = y_cs / q;

                double dp = PctPrime(x, k) / 100.0;
                double dydy = (q - y_cs * dp) / (q * q);

                double s_in = (double.IsNaN(sigma) ? 0.0 : sigma);
                double s_ml = Math.Abs(dydy) * s_in;

                y_cs = y_ml;
                sigma = s_ml;
            }

            public static void CorrectV50(ref double y, ref double s) { Apply(ref y, ref s, V50); }
            public static void CorrectV60(ref double y, ref double s) { Apply(ref y, ref s, V60); }
            public static void CorrectV667(ref double y, ref double s) { Apply(ref y, ref s, V667); }
        }

        // ---------- Unclustered Individual (single-output tolerant) ----------
        private sealed class SingleOutputModel_SO
        {
            public double LearningRate = 0.1;
            public double InitialPrediction = 0.0;
            public bool GoLeftIfLE = true;
            public double ComparatorEps = 0.0;
            public List<string> FeatureNames = new List<string>();
            public List<List<Dictionary<string, object>>> Trees = new List<List<Dictionary<string, object>>>();
        }
        private static int MapSplitFeatureTokenToIndex(string splitFeature)
        {
            if (string.IsNullOrEmpty(splitFeature)) return -1;
            var t = splitFeature.Trim();
            if (t.Length >= 2 && (t[0] == 'x' || t[0] == 'X'))
            {
                int k;
                if (int.TryParse(t.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && k >= 1)
                    return k - 1;
            }
            return -1;
        }
        private static Dictionary<string, object> ParseNode_SO(string node)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            dict["IsLeaf"] = J.ExtractBoolCI(node, new[] { "\"IsLeaf\"", "'IsLeaf'" }, false);
            dict["NodeId"] = J.ExtractIntCI(node, new[] { "\"NodeId\"", "'NodeId'" }, 1);
            dict["LeftChild"] = J.ExtractIntCI(node, new[] { "\"LeftChild\"", "'LeftChild'" }, 0);
            dict["RightChild"] = J.ExtractIntCI(node, new[] { "\"RightChild\"", "'RightChild'" }, 0);
            dict["FeatureIndex"] = J.ExtractIntCI(node, new[] { "\"FeatureIndex\"", "'FeatureIndex'" }, 0);
            dict["SplitFeature"] = J.ExtractStringCI(node, new[] { "\"SplitFeature\"", "'SplitFeature'" }) ?? "";
            dict["ThresholdD"] = J.ExtractDoubleCI(node, new[] { "\"Threshold\"", "'Threshold'" }, double.NaN);
            double mv = J.ExtractDoubleCI(node, new[] { "\"MultiValue\"", "'MultiValue'", "\"Value\"", "'Value'" }, 0.0);
            double ls = J.ExtractDoubleCI(node, new[] { "\"LeafStd\"", "'LeafStd'", "\"RegionStd\"", "'RegionStd'", "\"RegionStdPerOutput\"", "'RegionStdPerOutput'" }, 0.0);
            dict["LeafValue"] = mv;
            dict["LeafStd"] = ls;
            return dict;
        }
        private static bool GetBool(Dictionary<string, object> d, string k, bool defV)
        {
            object v; if (!d.TryGetValue(k, out v) || v == null) return defV;
            if (v is bool) return (bool)v; return defV;
        }
        private static int GetInt(Dictionary<string, object> d, string k, int defV)
        {
            object v; if (!d.TryGetValue(k, out v) || v == null) return defV;
            if (v is int) return (int)v;
            if (v is double) return (int)Math.Round((double)v);
            return defV;
        }
        private static double GetDouble(Dictionary<string, object> d, string k, double defV)
        {
            object v; if (!d.TryGetValue(k, out v) || v == null) return defV;
            if (v is double) return (double)v;
            if (v is int) return (int)v;
            return defV;
        }
        private static string GetString(Dictionary<string, object> d, string k, string defV)
        {
            object v; if (!d.TryGetValue(k, out v) || v == null) return defV;
            if (v is string) return (string)v;
            return defV;
        }
        private static bool TryParseSingle_SO(string json, out SingleOutputModel_SO so)
        {
            so = new SingleOutputModel_SO();
            try
            {
                string s = (json ?? "").Replace("\r\n", "\n");
                so.LearningRate = J.ExtractDoubleCI(s, new[] { "\"LearningRate\"", "'LearningRate'" }, 0.1);
                so.InitialPrediction = J.ExtractDoubleCI(s, new[] { "\"InitialPrediction\"", "'InitialPrediction'" }, 0.0);
                so.GoLeftIfLE = J.ExtractBoolCI(s, new[] { "\"GoLeftIfLessOrEqual\"", "'GoLeftIfLessOrEqual'" }, true);
                so.ComparatorEps = J.ExtractDoubleCI(s, new[] { "\"ComparatorEpsilon\"", "'ComparatorEpsilon'" }, 0.0);
                var feats = J.ExtractStringArrayCI(s, new[] { "\"FeatureNames\"", "'FeatureNames'" });
                if (feats != null) so.FeatureNames = feats;

                int ti = J.IndexAfterCI(s, new[] { "\"Trees\"", "'Trees'" });
                string tBlock = (ti >= 0) ? J.ExtractBlock(s, ti, '[', ']') : "[]";
                string tInner = J.TrimOuter(tBlock, '[', ']');

                var treeArrays = new List<string>();
                int i = 0, depth = 0, start = -1;
                while (i < tInner.Length)
                {
                    char c = tInner[i];
                    if (c == '[') { if (depth == 0) start = i; depth++; }
                    else if (c == ']') { depth--; if (depth == 0 && start >= 0) { treeArrays.Add(tInner.Substring(start, i - start + 1)); start = -1; } }
                    else if (c == '"') { i++; while (i < tInner.Length) { if (tInner[i] == '"' && tInner[i - 1] != '\\') break; i++; } }
                    i++;
                }

                foreach (var arr in treeArrays)
                {
                    string nodesTxt = J.TrimOuter(arr, '[', ']');
                    var nodes = new List<Dictionary<string, object>>();
                    int j = 0, d2 = 0, s0 = -1;
                    while (j < nodesTxt.Length)
                    {
                        char c = nodesTxt[j];
                        if (c == '{') { if (d2 == 0) s0 = j; d2++; }
                        else if (c == '}') { d2--; if (d2 == 0 && s0 >= 0) { nodes.Add(ParseNode_SO(nodesTxt.Substring(s0, j - s0 + 1))); s0 = -1; } }
                        else if (c == '"') { j++; while (j < nodesTxt.Length) { if (nodesTxt[j] == '"' && nodesTxt[j - 1] != '\\') break; j++; } }
                        j++;
                    }
                    so.Trees.Add(nodes);
                }
                return true;
            }
            catch { return false; }
        }
        private static void PredictSingleOutput_SO(SingleOutputModel_SO so, double[] X, out double y, out double var)
        {
            double sum = so.InitialPrediction;
            double v = 0.0;

            foreach (var tree in so.Trees)
            {
                var id2idx = new Dictionary<int, int>();
                for (int i = 0; i < tree.Count; i++)
                {
                    int nid = GetInt(tree[i], "NodeId", i + 1);
                    if (!id2idx.ContainsKey(nid)) id2idx[nid] = i;
                }

                int curId = tree.Count > 0 ? GetInt(tree[0], "NodeId", 1) : 1;
                int guard = 0;
                while (guard++ < 4096)
                {
                    int idx;
                    if (!id2idx.TryGetValue(curId, out idx)) break;
                    var node = tree[idx];
                    bool leaf = GetBool(node, "IsLeaf", false);
                    if (leaf)
                    {
                        double val = GetDouble(node, "LeafValue", 0.0);
                        double std = Math.Max(0.0, GetDouble(node, "LeafStd", 0.0));
                        sum += so.LearningRate * val;
                        v += (so.LearningRate * so.LearningRate) * (std * std);
                        break;
                    }
                    string sf = GetString(node, "SplitFeature", "");
                    int fFromSF = MapSplitFeatureTokenToIndex(sf);
                    int fIdxRaw = GetInt(node, "FeatureIndex", 0);
                    int f = (fFromSF >= 0) ? fFromSF : ((fIdxRaw > 0) ? (fIdxRaw - 1) : 0);

                    double thr = GetDouble(node, "ThresholdD", 0.0);
                    double xv = (f >= 0 && f < X.Length) ? X[f] : 0.0;

                    bool goLeft = so.GoLeftIfLE ? (xv <= thr + 1e-12) : (xv < thr - 1e-12);
                    int nextId = goLeft ? GetInt(node, "LeftChild", 0) : GetInt(node, "RightChild", 0);
                    if (nextId == 0) break;
                    curId = nextId;
                }
            }

            y = sum;
            var = Math.Max(0.0, v);
        }
        private static double[] MapFeaturesByName(List<string> names,
                                                  int noTumors, double rxGy, double totalVol,
                                                  double avgTumorVol, double avgSa_cm2, double avgEq_cm2,
                                                  double minDist_cm)
        {
            if (names == null) names = new List<string>();
            var X = new double[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string key = (names[i] ?? "").Trim();
                double v = 0.0;

                if (key.Equals("NoTumors", StringComparison.OrdinalIgnoreCase)) v = noTumors;
                else if (key.Equals("TotalDose", StringComparison.OrdinalIgnoreCase)) v = rxGy;
                else if (key.Equals("TotalVolume", StringComparison.OrdinalIgnoreCase)) v = totalVol;
                else if (key.Equals("AvgTumorVolume", StringComparison.OrdinalIgnoreCase)) v = avgTumorVol;
                else if (key.Equals("AvgSurfaceArea", StringComparison.OrdinalIgnoreCase)) v = avgSa_cm2;
                else if (key.Equals("AvgEquivSA", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("AvgEqSphereSA", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("AvgEqSA", StringComparison.OrdinalIgnoreCase)) v = avgEq_cm2;
                else if (key.Equals("MinShortestDistance", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("MinInterTumorDistance", StringComparison.OrdinalIgnoreCase)) v = minDist_cm;

                X[i] = v;
            }
            return X;
        }
        private static bool TryPredictSingle_SO(string json, double[] feat7, out double y, out double s)
        {
            y = double.NaN; s = double.NaN;
            SingleOutputModel_SO so;
            if (!TryParseSingle_SO(json, out so)) return false;

            int nT = (int)feat7[0];
            double rx = feat7[1];
            double tot = feat7[2];
            double avgTum = feat7[3];
            double avgSa = feat7[4];
            double avgEq = feat7[5];
            double minDist = feat7[6];

            var X = MapFeaturesByName(so.FeatureNames, nT, rx, tot, avgTum, avgSa, avgEq, minDist);
            double yy, vv; PredictSingleOutput_SO(so, X, out yy, out vv);
            y = yy; s = Math.Sqrt(Math.Max(0.0, vv));
            return true;
        }

        // ---------- Clustered 3-in-1 / Individual (C31) ----------
        private sealed class C31Node
        {
            public bool IsLeaf;
            public int NodeId;
            public int LeftChild;
            public int RightChild;
            public int FeatureIndex;      // 1-based
            public string SplitFeature;   // "xK"
            public double Threshold;
            public double LeafValue;
            public double LeafStd;
        }
        private sealed class C31Output
        {
            public double LearningRate = 0.1;
            public double InitialPrediction = 0.0;
            public bool GoLeftIfLE = true;
            public double ComparatorEps = 0.0;
            public List<string> FeatureNames = new List<string>();
            public List<List<C31Node>> Trees = new List<List<C31Node>>();
        }
        private sealed class C31Model
        {
            public C31Output[] Outputs = new C31Output[3];
            public bool ClampNN = true;
            public bool CapAtRef = true;
            public double[] Weights = new[] { 0.5, 1.0, 0.5 };
            public double Epsilon = 1e-6;
            public bool Strict = false;
        }
        private static C31Node ParseNode_C31(string node)
        {
            return new C31Node
            {
                IsLeaf = J.ExtractBoolCI(node, new[] { "\"IsLeaf\"", "'IsLeaf'" }, false),
                NodeId = J.ExtractIntCI(node, new[] { "\"NodeId\"", "'NodeId'" }, 1),
                LeftChild = J.ExtractIntCI(node, new[] { "\"LeftChild\"", "'LeftChild'" }, 0),
                RightChild = J.ExtractIntCI(node, new[] { "\"RightChild\"", "'RightChild'" }, 0),
                FeatureIndex = J.ExtractIntCI(node, new[] { "\"FeatureIndex\"", "'FeatureIndex'" }, 0),
                SplitFeature = J.ExtractStringCI(node, new[] { "\"SplitFeature\"", "'SplitFeature'" }) ?? "",
                Threshold = J.ExtractDoubleCI(node, new[] { "\"Threshold\"", "'Threshold'" }, 0.0),
                LeafValue = J.ExtractDoubleCI(node, new[] { "\"MultiValue\"", "'MultiValue'", "\"Value\"", "'Value'" }, 0.0),
                LeafStd = J.ExtractDoubleCI(node, new[] { "\"LeafStd\"", "'LeafStd'", "\"RegionStd\"", "'RegionStd'", "\"RegionStdPerOutput\"", "'RegionStdPerOutput'" }, 0.0)
            };
        }
        private static bool TryParseC31(string json, out C31Model model)
        {
            model = new C31Model();
            try
            {
                string s = (json ?? "").Replace("\r\n", "\n");

                int ppi = J.IndexAfterCI(s, new[] { "\"PostProcessing\"", "'PostProcessing'", "\"Postprocess\"", "'Postprocess'" });
                if (ppi >= 0)
                {
                    string pp = J.ExtractBlock(s, ppi, '{', '}');
                    model.ClampNN = J.ExtractBoolCI(pp, new[] { "\"ClampNonNegative\"", "'ClampNonNegative'" }, true);
                    model.CapAtRef = J.ExtractBoolCI(pp, new[] { "\"CapAtReference\"", "'CapAtReference'" }, true);
                    model.Strict = J.ExtractBoolCI(pp, new[] { "\"Strict\"", "'Strict'" }, false);
                    double e = J.ExtractDoubleCI(pp, new[] { "\"Epsilon\"", "'Epsilon'" }, 1e-6);
                    model.Epsilon = (e > 0) ? e : 1e-6;
                    var wv = J.ExtractDoubleArrayCI(pp, new[] { "\"Weights\"", "'Weights'" });
                    if (wv != null && wv.Length >= 3) model.Weights = new[] { wv[0], wv[1], wv[2] };
                }

                int oe = J.IndexAfterCI(s, new[] { "\"OutputEnsembles\"", "'OutputEnsembles'" });
                if (oe < 0) return false;
                string block = J.ExtractBlock(s, oe, '[', ']');
                string inner = J.TrimOuter(block, '[', ']');

                var outputs = new List<string>();
                {
                    int i = 0, depth = 0, start = -1;
                    while (i < inner.Length)
                    {
                        char c = inner[i];
                        if (c == '{') { if (depth == 0) start = i; depth++; }
                        else if (c == '}') { depth--; if (depth == 0 && start >= 0) { outputs.Add(inner.Substring(start, i - start + 1)); start = -1; } }
                        else if (c == '"') { i++; while (i < inner.Length) { if (inner[i] == '"' && inner[i - 1] != '\\') break; i++; } }
                        i++;
                    }
                }
                if (outputs.Count == 0) return false;

                int outCount = Math.Min(3, outputs.Count);
                for (int oi = 0; oi < outCount; oi++)
                {
                    string e = outputs[oi];
                    var so = new C31Output();
                    so.LearningRate = J.ExtractDoubleCI(e, new[] { "\"LearningRate\"", "'LearningRate'" }, 0.1);
                    so.InitialPrediction = J.ExtractDoubleCI(e, new[] { "\"InitialPrediction\"", "'InitialPrediction'" }, 0.0);
                    so.GoLeftIfLE = J.ExtractBoolCI(e, new[] { "\"GoLeftIfLessOrEqual\"", "'GoLeftIfLessOrEqual'" }, true);
                    so.ComparatorEps = J.ExtractDoubleCI(e, new[] { "\"ComparatorEpsilon\"", "'ComparatorEpsilon'" }, 0.0);
                    var feats = J.ExtractStringArrayCI(e, new[] { "\"FeatureNames\"", "'FeatureNames'" });
                    if (feats != null) so.FeatureNames = feats;

                    int tIdx = J.IndexAfterCI(e, new[] { "\"Trees\"", "'Trees'" });
                    string tBlock = (tIdx >= 0) ? J.ExtractBlock(e, tIdx, '[', ']') : "[]";
                    string tInner = J.TrimOuter(tBlock, '[', ']');

                    var treeArrays = new List<string>();
                    {
                        int i = 0, depth = 0, start = -1;
                        while (i < tInner.Length)
                        {
                            char c = tInner[i];
                            if (c == '[') { if (depth == 0) start = i; depth++; }
                            else if (c == ']') { depth--; if (depth == 0 && start >= 0) { treeArrays.Add(tInner.Substring(start, i - start + 1)); start = -1; } }
                            else if (c == '"') { i++; while (i < tInner.Length) { if (tInner[i] == '"' && tInner[i - 1] != '\\') break; i++; } }
                            i++;
                        }
                    }

                    foreach (var arr in treeArrays)
                    {
                        string nodesTxt = J.TrimOuter(arr, '[', ']');
                        var nodes = new List<C31Node>();
                        int i = 0, depth = 0, start = -1;
                        while (i < nodesTxt.Length)
                        {
                            char c = nodesTxt[i];
                            if (c == '{') { if (depth == 0) start = i; depth++; }
                            else if (c == '}') { depth--; if (depth == 0 && start >= 0) { nodes.Add(ParseNode_C31(nodesTxt.Substring(start, i - start + 1))); start = -1; } }
                            else if (c == '"') { i++; while (i < nodesTxt.Length) { if (nodesTxt[i] == '"' && nodesTxt[i - 1] != '\\') break; i++; } }
                            i++;
                        }
                        so.Trees.Add(nodes);
                    }

                    model.Outputs[oi] = so;
                }

                return true;
            }
            catch { return false; }
        }
        private static void PredictOneOutputC31(C31Output so, double[] X, out double y, out double var)
        {
            double sum = so.InitialPrediction;
            double v = 0.0;

            foreach (var tree in so.Trees)
            {
                var id2idx = new Dictionary<int, int>();
                for (int i = 0; i < tree.Count; i++)
                {
                    int nid = tree[i].NodeId;
                    if (!id2idx.ContainsKey(nid)) id2idx[nid] = i;
                }

                int curId = tree.Count > 0 ? tree[0].NodeId : 1;
                int guard = 0;
                while (guard++ < 4096)
                {
                    int idx;
                    if (!id2idx.TryGetValue(curId, out idx)) break;
                    var node = tree[idx];

                    if (node.IsLeaf)
                    {
                        sum += so.LearningRate * node.LeafValue;
                        double std = Math.Max(0.0, node.LeafStd);
                        v += (so.LearningRate * so.LearningRate) * (std * std);
                        break;
                    }

                    int f = -1;
                    if (!string.IsNullOrEmpty(node.SplitFeature))
                    {
                        int fFromSF = MapSplitFeatureTokenToIndex(node.SplitFeature);
                        if (fFromSF >= 0) f = fFromSF;
                    }
                    if (f < 0 && node.FeatureIndex > 0) f = node.FeatureIndex - 1;

                    double thr = node.Threshold;
                    double xv = (f >= 0 && f < X.Length) ? X[f] : 0.0;
                    bool goLeft = so.GoLeftIfLE ? (xv <= thr + so.ComparatorEps) : (xv < thr - so.ComparatorEps);
                    int nextId = goLeft ? node.LeftChild : node.RightChild;
                    if (nextId == 0) break;
                    curId = nextId;
                }
            }

            y = sum;
            var = Math.Max(0.0, v);
        }
        private static bool TryPredictC31(string json, double[] feat7, double refVolCc, out double[] yFinal, out double[] sigma)
        {
            yFinal = new[] { double.NaN, double.NaN, double.NaN };
            sigma = new[] { double.NaN, double.NaN, double.NaN };

            C31Model m;
            if (!TryParseC31(json, out m)) return false;

            int nT = (int)feat7[0];
            double rx = feat7[1];
            double tot = feat7[2];
            double avgTum = feat7[3];
            double avgSa = feat7[4];
            double avgEq = feat7[5];
            double minDist = feat7[6];

            var y = new double[3]; var var_ = new double[3];
            for (int oi = 0; oi < 3; oi++)
            {
                var so = m.Outputs[oi];
                if (so == null) { y[oi] = double.NaN; var_[oi] = double.NaN; continue; }

                var X = MapFeaturesByName(so.FeatureNames, nT, rx, tot, avgTum, avgSa, avgEq, minDist);
                double yy, vv; PredictOneOutputC31(so, X, out yy, out vv);
                y[oi] = yy; var_[oi] = vv;
            }

            if (m.ClampNN) for (int i = 0; i < 3; i++) if (!double.IsNaN(y[i]) && y[i] < 0) y[i] = 0;
            if (m.CapAtRef && refVolCc > 0) for (int i = 0; i < 3; i++) if (!double.IsNaN(y[i]) && y[i] > refVolCc) y[i] = refVolCc;

            // monotone V50>=V60>=V66.7
            if (y[0] < y[1]) y[1] = y[0];
            if (y[1] < y[2]) y[2] = y[1];

            yFinal = y;
            sigma = new[] { Math.Sqrt(var_[0]), Math.Sqrt(var_[1]), Math.Sqrt(var_[2]) };
            return true;
        }

        // ===== MINI clustered 3-in-1 exact logic =====
        internal sealed class MiniPostprocessConfig
        {
            public bool ClampNonNegative = true;
            public bool CapAtReference = true;
            public double[] Weights = new double[] { 0.5, 1.0, 0.5 };
            public double Epsilon = 1e-6;
            public bool Strict = false;
        }
        internal sealed class MiniTreeNodeSO
        {
            public bool IsLeaf;
            public int FeatureIndex;   // 0-based
            public double Threshold;
            public int LeftIndex;
            public int RightIndex;
            public double LeafValue;   // scalar
            public double LeafStd;     // optional
        }
        internal sealed class MiniTreeSO
        {
            public List<MiniTreeNodeSO> Nodes = new List<MiniTreeNodeSO>();
            public int RootIndex = 0;
        }
        internal sealed class MiniSingleOutputModel
        {
            public string OutputName = "";
            public double LearningRate = 0.1;
            public double InitialPrediction = 0.0;
            public List<string> FeatureNames = new List<string>();
            public List<MiniTreeSO> Trees = new List<MiniTreeSO>();
            public bool GoLeftIfLessOrEqual = true;
            public double ComparatorEpsilon = 0.0;
        }
        internal sealed class MiniMultiModel
        {
            public List<MiniSingleOutputModel> Outputs = new List<MiniSingleOutputModel>(); // expect 3
            public MiniPostprocessConfig Postprocess = null;
            public bool GlobalGoLeftIfLE = true;
        }

        internal static class MiniModelParser
        {
            public static bool TryParse(string json, out MiniMultiModel multi)
            {
                multi = new MiniMultiModel();
                try
                {
                    string s = (json ?? "").Replace("\r\n", "\n");

                    MiniPostprocessConfig pp = null;
                    int ppA = J.IndexAfterCI(s, new[] { "\"PostProcessing\"", "'PostProcessing'" });
                    if (ppA >= 0) pp = ParsePP(J.ExtractBlock(s, ppA, '{', '}'));
                    if (pp == null)
                    {
                        int ppB = J.IndexAfterCI(s, new[] { "\"Postprocess\"", "'Postprocess'" });
                        if (ppB >= 0) pp = ParsePP(J.ExtractBlock(s, ppB, '{', '}'));
                    }
                    multi.Postprocess = pp;

                    int sp = J.IndexAfterCI(s, new[] { "\"SplitRules\"", "'SplitRules'" });
                    if (sp >= 0)
                    {
                        string b = J.ExtractBlock(s, sp, '{', '}');
                        multi.GlobalGoLeftIfLE = J.ExtractBoolCI(b, new[] { "\"GoLeftIfLessOrEqual\"", "'GoLeftIfLessOrEqual'" }, true);
                    }
                    else multi.GlobalGoLeftIfLE = true;

                    string oeBlock = "";
                    int ens = J.IndexAfterCI(s, new[] { "\"Ensemble\"", "'Ensemble'" });
                    if (ens >= 0)
                    {
                        string eblk = J.ExtractBlock(s, ens, '{', '}');
                        int oe = J.IndexAfterCI(eblk, new[] { "\"OutputEnsembles\"", "'OutputEnsembles'" });
                        if (oe < 0) return false;
                        oeBlock = J.ExtractBlock(eblk, oe, '[', ']');
                    }
                    else
                    {
                        int oe = J.IndexAfterCI(s, new[] { "\"OutputEnsembles\"", "'OutputEnsembles'" });
                        if (oe < 0) return false;
                        oeBlock = J.ExtractBlock(s, oe, '[', ']');
                    }

                    string inner = J.TrimOuter(oeBlock, '[', ']');
                    var ensembles = ExtractTopLevelObjects(inner);
                    if (ensembles.Count == 0) return false;

                    foreach (var e in ensembles)
                    {
                        var m = new MiniSingleOutputModel();
                        m.LearningRate = J.ExtractDoubleCI(e, new[] { "\"LearningRate\"", "'LearningRate'" }, 0.1);
                        m.InitialPrediction = J.ExtractDoubleCI(e, new[] { "\"InitialPrediction\"", "'InitialPrediction'" }, 0.0);
                        m.GoLeftIfLessOrEqual = J.ExtractBoolCI(e, new[] { "\"GoLeftIfLessOrEqual\"", "'GoLeftIfLessOrEqual'" }, multi.GlobalGoLeftIfLE);
                        m.ComparatorEpsilon = J.ExtractDoubleCI(e, new[] { "\"ComparatorEpsilon\"", "'ComparatorEpsilon'" }, 0.0);
                        var feats = J.ExtractStringArrayCI(e, new[] { "\"FeatureNames\"", "'FeatureNames'" });
                        if (feats != null) m.FeatureNames = feats;

                        int tIdx = J.IndexAfterCI(e, new[] { "\"Trees\"", "'Trees'" });
                        string tBlock = (tIdx >= 0) ? J.ExtractBlock(e, tIdx, '[', ']') : "[]";
                        var treeArrays = ExtractTopLevelArrays(J.TrimOuter(tBlock, '[', ']'));

                        foreach (var arr in treeArrays)
                        {
                            string nodesTxt = J.TrimOuter(arr, '[', ']');
                            var nodes = ExtractTopLevelObjects(nodesTxt);

                            var t = new MiniTreeSO();
                            var id2idx = new Dictionary<int, int>();
                            for (int i = 0; i < nodes.Count; i++)
                            {
                                int nid = J.ExtractIntCI(nodes[i], new[] { "\"NodeId\"", "'NodeId'" }, i + 1);
                                if (!id2idx.ContainsKey(nid)) id2idx[nid] = i;
                            }
                            for (int i = 0; i < nodes.Count; i++)
                            {
                                string nobj = nodes[i];
                                var n = new MiniTreeNodeSO();
                                n.IsLeaf = J.ExtractBoolCI(nobj, new[] { "\"IsLeaf\"", "'IsLeaf'" }, false);

                                int f = -1;
                                string splitName = J.ExtractStringCI(nobj, new[] { "\"SplitFeature\"", "'SplitFeature'" });
                                if (!string.IsNullOrEmpty(splitName))
                                {
                                    if (m.FeatureNames != null && m.FeatureNames.Count > 0)
                                    {
                                        int byName = m.FeatureNames.IndexOf(splitName);
                                        if (byName >= 0) f = byName;
                                    }
                                    if (f < 0)
                                    {
                                        var sf = splitName.Trim();
                                        if (sf.Length >= 2 && (sf[0] == 'x' || sf[0] == 'X'))
                                        {
                                            int k; if (int.TryParse(sf.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && k >= 1) f = k - 1;
                                        }
                                    }
                                }
                                if (f < 0)
                                {
                                    int f1 = J.ExtractIntCI(nobj, new[] { "\"FeatureIndex\"", "'FeatureIndex'" }, 0);
                                    if (f1 > 0) f = f1 - 1;
                                }
                                n.FeatureIndex = f;

                                n.Threshold = J.ExtractDoubleCI(nobj, new[] { "\"Threshold\"", "'Threshold'" }, 0.0);

                                int lId = J.ExtractIntCI(nobj, new[] { "\"LeftChild\"", "'LeftChild'" }, 0);
                                int rId = J.ExtractIntCI(nobj, new[] { "\"RightChild\"", "'RightChild'" }, 0);
                                // temporarily store ids; will map below
                                n.LeftIndex = lId;
                                n.RightIndex = rId;

                                if (n.IsLeaf)
                                {
                                    n.LeafValue = J.ExtractDoubleCI(nobj, new[] { "\"MultiValue\"", "'MultiValue'", "\"Value\"", "'Value'" }, 0.0);
                                    n.LeafStd = Math.Max(0.0, J.ExtractDoubleCI(nobj, new[] { "\"LeafStd\"", "'LeafStd'", "\"RegionStdPerOutput\"", "'RegionStdPerOutput'" }, 0.0));
                                }
                                t.Nodes.Add(n);
                            }
                            // Map NodeId to index for Left/Right
                            var id2 = new Dictionary<int, int>();
                            for (int i2 = 0; i2 < nodes.Count; i2++)
                            {
                                int nid = J.ExtractIntCI(nodes[i2], new[] { "\"NodeId\"", "'NodeId'" }, i2 + 1);
                                if (!id2.ContainsKey(nid)) id2[nid] = i2;
                            }
                            for (int i2 = 0; i2 < t.Nodes.Count; i2++)
                            {
                                var n = t.Nodes[i2];
                                int lId = n.LeftIndex, rId = n.RightIndex;
                                n.LeftIndex = (lId > 0 && id2.ContainsKey(lId)) ? id2[lId] : -1;
                                n.RightIndex = (rId > 0 && id2.ContainsKey(rId)) ? id2[rId] : -1;
                            }
                            t.RootIndex = id2.ContainsKey(1) ? id2[1] : 0;

                            // add tree to current output model
                            m.Trees.Add(t);
                        }

                        // add the complete output model
                        multi.Outputs.Add(m);
                    }

                    return multi.Outputs.Count > 0;
                }
                catch { multi = null; return false; }
            }

            private static MiniPostprocessConfig ParsePP(string block)
            {
                if (string.IsNullOrEmpty(block)) return null;
                var pp = new MiniPostprocessConfig();
                pp.ClampNonNegative = J.ExtractBoolCI(block, new[] { "\"ClampNonNegative\"", "'ClampNonNegative'" }, true);
                pp.CapAtReference = J.ExtractBoolCI(block, new[] { "\"CapAtReference\"", "'CapAtReference'" }, true);

                int mono = J.IndexAfterCI(block, new[] { "\"Monotone\"", "'Monotone'" });
                if (mono >= 0)
                {
                    string m = J.ExtractBlock(block, mono, '{', '}');
                    var w = J.ExtractDoubleArrayCI(m, new[] { "\"Weights\"", "'Weights'" });
                    if (w != null && w.Length >= 3) pp.Weights = new[] { w[0], w[1], w[2] };
                    double eps = J.ExtractDoubleCI(m, new[] { "\"Epsilon\"", "'Epsilon'" }, 1e-6);
                    pp.Epsilon = (eps > 0) ? eps : 1e-6;
                    pp.Strict = J.ExtractBoolCI(m, new[] { "\"Strict\"", "'Strict'" }, false);
                }
                return pp;
            }
            private static List<string> ExtractTopLevelObjects(string inner)
            {
                var res = new List<string>(); int i = 0, depth = 0, start = -1;
                while (i < inner.Length)
                {
                    char c = inner[i];
                    if (c == '{') { if (depth == 0) start = i; depth++; }
                    else if (c == '}') { depth--; if (depth == 0 && start >= 0) { res.Add(inner.Substring(start, i - start + 1)); start = -1; } }
                    else if (c == '"') { i++; while (i < inner.Length) { if (i < inner.Length && inner[i] == '"' && inner[i - 1] != '\\') break; i++; } }
                    i++;
                }
                return res;
            }
            private static List<string> ExtractTopLevelArrays(string inner)
            {
                var res = new List<string>(); int i = 0, depth = 0, start = -1;
                while (i < inner.Length)
                {
                    char c = inner[i];
                    if (c == '[') { if (depth == 0) start = i; depth++; }
                    else if (c == ']') { depth--; if (depth == 0 && start >= 0) { res.Add(inner.Substring(start, i - start + 1)); start = -1; } }
                    else if (c == '"') { i++; while (i < inner.Length) { if (i < inner.Length && inner[i] == '"' && inner[i - 1] != '\\') break; i++; } }
                    i++;
                }
                return res;
            }
        }

        internal static class MiniPredictor
        {
            public static double PredictOne(MiniSingleOutputModel m, double[] X, out double sigma)
            {
                double sum = m.InitialPrediction;
                double var = 0.0;

                for (int ti = 0; ti < m.Trees.Count; ti++)
                {
                    var tree = m.Trees[ti];
                    if (tree == null || tree.Nodes == null || tree.Nodes.Count == 0) continue;

                    int idx = tree.RootIndex;
                    int guard = 0;
                    while (idx >= 0 && idx < tree.Nodes.Count && guard++ < 2048)
                    {
                        var n = tree.Nodes[idx];
                        if (n.IsLeaf)
                        {
                            double lr = m.LearningRate;
                            sum += lr * n.LeafValue;
                            if (n.LeafStd > 0) var += (lr * lr) * (n.LeafStd * n.LeafStd);
                            break;
                        }
                        int f = n.FeatureIndex;
                        double xv = (f >= 0 && f < X.Length) ? X[f] : 0.0;
                        bool goLeft = m.GoLeftIfLessOrEqual ? (xv <= n.Threshold + m.ComparatorEpsilon)
                                                            : (xv < n.Threshold - m.ComparatorEpsilon);
                        idx = goLeft ? n.LeftIndex : n.RightIndex;
                        if (idx < 0) break;
                    }
                }

                sigma = Math.Sqrt(Math.Max(0.0, var));
                return sum;
            }

            public static void Isotonic3InPlace(double[] y, double[] w, double eps, bool strict, bool clampNonNeg)
            {
                if (y == null || y.Length < 3) return;
                if (w == null || w.Length < 3) { w = new[] { 0.5, 1.0, 0.5 }; }
                double m0 = y[0], m1 = y[1], m2 = y[2];
                double W0 = w[0], W1 = w[1], W2 = w[2];

                if (m0 < m1 || m1 < m2)
                {
                    if (m0 < m1)
                    {
                        double W01 = W0 + W1;
                        double m01 = (W0 * m0 + W1 * m1) / (W01 == 0 ? 1.0 : W01);
                        m0 = m01; W0 = W01;
                    }
                    if (m1 < m2)
                    {
                        double W12 = W1 + W2;
                        double m12 = (W1 * m1 + W2 * m2) / (W12 == 0 ? 1.0 : W12);
                        if (m0 < m12)
                        {
                            double W012 = W0 + W12;
                            double m012 = (W0 * m0 + W12 * m12) / (W012 == 0 ? 1.0 : W012);
                            m0 = m1 = m2 = m012;
                        }
                        else m1 = m2 = m12;
                    }
                    y[0] = m0; y[1] = m1; y[2] = m2;
                }
                if (eps > 0) { if (y[0] < y[1]) y[1] = y[0]; if (y[1] < y[2]) y[2] = y[1]; }
                if (strict)
                {
                    double scale = Math.Max(1e-6, Math.Max(Math.Abs(y[0]), Math.Max(Math.Abs(y[1]), Math.Abs(y[2]))));
                    double tiny = eps * scale;
                    if (y[0] < y[1]) y[1] = Math.Min(y[0] - tiny, y[1]);
                    if (y[1] < y[2]) y[2] = Math.Min(y[1] - tiny, y[2]);
                    if (clampNonNeg && y[2] < 0) y[2] = 0;
                }
            }
        }
        // Add this inside class Script (e.g., just below MiniPredictor)
        private static bool TryPredictC31_Mini(
            string json,
            List<string>[] perOutputFeatureNames,
            double[] feat7,
            double refVolCc,
            out double[] y,
            out double[] sig)
        {
            y = new[] { double.NaN, double.NaN, double.NaN };
            sig = new[] { double.NaN, double.NaN, double.NaN };

            MiniMultiModel multi;
            if (!MiniModelParser.TryParse(json, out multi) ||
                multi == null || multi.Outputs == null || multi.Outputs.Count == 0)
                return false;

            int nT = (int)feat7[0];
            double rx = feat7[1];
            double tot = feat7[2];
            double avgTum = feat7[3];
            double avgSa = feat7[4];
            double avgEq = feat7[5];
            double minDist = feat7[6];

            for (int i = 0; i < 3 && i < multi.Outputs.Count; i++)
            {
                var so = multi.Outputs[i];
                var names =
                    (perOutputFeatureNames != null && i < perOutputFeatureNames.Length && perOutputFeatureNames[i] != null)
                    ? perOutputFeatureNames[i]
                    : so.FeatureNames;

                var X = MapFeaturesByName(names, nT, rx, tot, avgTum, avgSa, avgEq, minDist);

                double s_i;
                y[i] = MiniPredictor.PredictOne(so, X, out s_i);
                sig[i] = s_i;
            }

            var pp = multi.Postprocess ?? new MiniPostprocessConfig();

            // Clamp non-negative and cap at reference volume if requested
            if (pp.ClampNonNegative)
                for (int i = 0; i < 3; i++)
                    if (!double.IsNaN(y[i]) && y[i] < 0) y[i] = 0;

            if (pp.CapAtReference && refVolCc > 0)
                for (int i = 0; i < 3; i++)
                    if (!double.IsNaN(y[i]) && y[i] > refVolCc) y[i] = refVolCc;

            // Enforce V50 >= V60 >= V66.7 with isotonic smoothing
            MiniPredictor.Isotonic3InPlace(
                y,
                pp.Weights ?? new[] { 0.5, 1.0, 0.5 },
                pp.Epsilon,
                pp.Strict,
                pp.ClampNonNegative);

            return true;
        }

        private static bool NearlySame(double a, double b, double eps = 1e-9)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) <= eps;
        }

        private static TextBlock ValCellNA(double v, double s, bool highlight = false)
        {
            string txt = double.IsNaN(v)
                ? "NA"
                : (double.IsNaN(s)
                    ? v.ToString("G6", CultureInfo.InvariantCulture)
                    : (v.ToString("G6", CultureInfo.InvariantCulture) + " ± " + s.ToString("G6", CultureInfo.InvariantCulture)));

            return new TextBlock
            {
                Text = txt,
                Margin = new Thickness(2, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = highlight ? Brushes.ForestGreen : Brushes.Black
            };
        }

        // ---------- SMALL PLOT (points + error bars) ----------
        private class MiniPlot : FrameworkElement
        {
            public double[] Values = new double[] { double.NaN, double.NaN, double.NaN }; // V50,V60,V66.7
            public double[] Sigmas = new double[] { 0.0, 0.0, 0.0 };
            public Thickness PlotMargin = new Thickness(32, 6, 6, 20);
            public double YMinBound = double.NaN;  // optional fixed bounds
            public double YMaxBound = double.NaN;

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                if (Values == null || Values.Length < 3) return;

                double w = Math.Max(ActualWidth, 1);
                double h = Math.Max(ActualHeight, 1);

                double L = PlotMargin.Left, T = PlotMargin.Top;
                double Rm = PlotMargin.Right, Bm = PlotMargin.Bottom;
                double plotW = Math.Max(10, w - L - Rm);
                double plotH = Math.Max(10, h - T - Bm);

                double[] v = new double[3];
                double[] s = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    v[i] = double.IsNaN(Values[i]) ? 0.0 : Math.Max(0.0, Values[i]);
                    s[i] = (Sigmas != null && Sigmas.Length > i && !double.IsNaN(Sigmas[i])) ? Math.Max(0.0, Sigmas[i]) : 0.0;
                }

                double vmin = v.Min(), vmax = v.Max();
                if (double.IsNaN(YMinBound))
                    YMinBound = Math.Max(0.0, 0.9 * vmin);
                if (double.IsNaN(YMaxBound))
                    YMaxBound = Math.Max(YMinBound + 1e-6, 1.1 * vmax);

                double ymn = YMinBound;
                double ymx = YMaxBound;

                Func<double, double> Xto = (x01) => L + x01 * plotW;
                Func<double, double> Yto = (yval) => T + (ymx - yval) * plotH / (ymx - ymn + 1e-12);

                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

                var axisPen = new Pen(Brushes.Gray, 1.0); axisPen.Freeze();
                var dashPen = new Pen(Brushes.DarkGray, 1.0) { DashStyle = DashStyles.Dash }; dashPen.Freeze();
                var curvePen = new Pen(Brushes.SteelBlue, 1.2) { DashStyle = DashStyles.Dot }; curvePen.Freeze();
                var errorPen = new Pen(Brushes.SteelBlue, 1.0); errorPen.Freeze();
                var pointBrush = Brushes.SteelBlue;

                dc.DrawLine(axisPen, new Point(L, T), new Point(L, T + plotH));

                double[] xCats = new double[] { 0.0, 0.5, 1.0 };
                string[] xLbl = new[] { "V50%", "V60%", "V66.7%" };

                double ppd = 1.0;
                try { ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { ppd = 1.0; }

                for (int i = 0; i < 3; i++)
                {
                    double yy = Yto(v[i]);
                    dc.DrawLine(axisPen, new Point(L - 4, yy), new Point(L, yy));
                    var ft = new FormattedText(
                        v[i].ToString("G5", CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 9, Brushes.DimGray, ppd);
                    dc.DrawText(ft, new Point(Math.Max(2.0, L - 6 - ft.Width), yy - ft.Height / 2.0));
                }

                for (int i = 0; i < 3; i++)
                {
                    double x = Xto(xCats[i]);
                    double y = Yto(v[i]);
                    dc.DrawLine(dashPen, new Point(L, y), new Point(x, y));
                }

                const double capHalf = 4.0;
                for (int i = 0; i < 3; i++)
                {
                    double x = Xto(xCats[i]);
                    double ylow = Yto(v[i] - s[i]);
                    double yhigh = Yto(v[i] + s[i]);

                    ylow = Math.Max(T, Math.Min(T + plotH, ylow));
                    yhigh = Math.Max(T, Math.Min(T + plotH, yhigh));

                    dc.DrawLine(errorPen, new Point(x, ylow), new Point(x, yhigh));
                    dc.DrawLine(errorPen, new Point(x - capHalf, ylow), new Point(x + capHalf, ylow));
                    dc.DrawLine(errorPen, new Point(x - capHalf, yhigh), new Point(x + capHalf, yhigh));
                }

                for (int i = 0; i < 3; i++)
                {
                    double x = Xto(xCats[i]);
                    double y = Yto(v[i]);
                    dc.DrawEllipse(pointBrush, null, new Point(x, y), 3.0, 3.0);
                }

                for (int i = 0; i < 3; i++)
                {
                    double x = Xto(xCats[i]);
                    var ft = new FormattedText(
                        xLbl[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 9, Brushes.DimGray, ppd);
                    dc.DrawText(ft, new Point(x - ft.Width / 2.0, T + plotH + 1));
                }

                // simple log-linear trend for eye
                double eps = 1e-6;
                double[] t = new double[] { 0.0, 1.0, 2.0 };
                double[] ly = new double[3];
                for (int i = 0; i < 3; i++) ly[i] = Math.Log(Math.Max(eps, v[i] + eps));
                double n = 3.0;
                double sumT = t.Sum();
                double sumTT = t.Select(z => z * z).Sum();
                double sumY = ly.Sum();
                double sumTY = 0.0; for (int i = 0; i < 3; i++) sumTY += t[i] * ly[i];
                double den = (n * sumTT - sumT * sumT);
                double a = 0.0, b = 0.0;
                if (Math.Abs(den) > 1e-12)
                {
                    b = (n * sumTY - sumT * sumY) / den;
                    a = (sumY - b * sumT) / n;
                }
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    int samples = 48;
                    for (int i = 0; i < samples; i++)
                    {
                        double tt = (2.0 * i) / (samples - 1); // 0..2
                        double yfit = Math.Exp(a + b * tt);
                        double x01 = tt / 2.0; // map 0..2 => 0..1
                        Point p = new Point(Xto(x01), Yto(yfit));
                        if (i == 0) gc.BeginFigure(p, false, false);
                        else gc.LineTo(p, true, false);
                    }
                }
                geo.Freeze();
                dc.DrawGeometry(null, curvePen, geo);
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(MiniPlotWidth, MiniPlotHeight);
            }
        }

        // Create a TextBlock like M₍a,s₎ (bold+italic, subscript under M)
        private static TextBlock MakeModelLabel(string subscript, double baseFontSize)
        {
            var tb = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.FontSize = baseFontSize + ModelFontDelta;

            tb.Inlines.Add(new Run("M"));
            var sub = new Run(subscript)
            {
                BaselineAlignment = BaselineAlignment.Subscript,
                FontSize = tb.FontSize * 0.75
            };
            tb.Inlines.Add(sub);
            return tb;
        }

        private static StackPanel MakeClusterBadge(int cid)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            Brush fill = Brushes.Red;
            if (cid == 2) fill = Brushes.Blue;
            else if (cid == 3) fill = Brushes.Green;

            p.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = fill, Margin = new Thickness(0, 0, 6, 0) });
            p.Children.Add(new TextBlock { Text = "(c" + cid + ")", VerticalAlignment = VerticalAlignment.Center });
            return p;
        }

        // ---------- MAIN ----------
        public void Execute(ScriptContext context)
        {
            try
            {
                if (context == null || context.StructureSet == null)
                {
                    MessageBox.Show("Open a patient with a valid Structure Set (and optionally a Plan).");
                    return;
                }

                // 0) Gather all Exp* structures
                var allExpLesions = context.StructureSet.Structures
                    .Where(s => !s.IsEmpty && s.Id.StartsWith("Exp", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // 1) If there are any, show selection dialog (include=checked)
                List<Structure> lesions;
                if (allExpLesions.Count > 0)
                {
                    var picker = new SelectExpLesionsWindow(allExpLesions);
                    bool? ok = picker.ShowDialog();
                    if (ok != true) return; // user cancelled
                    lesions = picker.GetIncludedLesions();
                }
                else
                {
                    lesions = new List<Structure>();
                }

                // 2) Compute features from INCLUDED lesions only
                int noTumors = lesions.Count;
                double totalVol = 0.0;

                // NEW: gather both actual mesh SA (if available) and EqSphere SA
                double sumSa_mm2 = 0.0;
                double sumEq_mm2 = 0.0;

                var lesionVolumesCc = new List<double>();
                var radii_mm = new List<double>();
                var centers = new List<VVector>();

                foreach (var s in lesions.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
                {
                    double vol_cc = s.Volume;
                    double vol_mm3 = vol_cc * 1000.0;
                    double r_mm = Math.Pow((3.0 * vol_mm3) / (4.0 * Math.PI), 1.0 / 3.0);

                    // Eq-sphere SA (mm^2)
                    double eqSa_mm2 = 4.0 * Math.PI * r_mm * r_mm;

                    // Actual mesh SA if available (mm^2); otherwise fallback to eq-sphere
                    double sa_mesh_mm2 = ComputeSurfaceAreaFromMesh(s);
                    if (sa_mesh_mm2 <= 0.0) sa_mesh_mm2 = eqSa_mm2;

                    totalVol += vol_cc;
                    sumEq_mm2 += eqSa_mm2;
                    sumSa_mm2 += sa_mesh_mm2;

                    lesionVolumesCc.Add(vol_cc);
                    radii_mm.Add(r_mm);
                    centers.Add(s.CenterPoint);
                }

                // Averages in cm^2 (consistent with mini script)
                double avgSa_cm2 = (noTumors > 0) ? ((sumSa_mm2 / 100.0) / Math.Max(1, noTumors)) : 0.0;
                double avgEq_cm2 = (noTumors > 0) ? ((sumEq_mm2 / 100.0) / Math.Max(1, noTumors)) : 0.0;

                double avgTumorVolCc = (noTumors > 0) ? totalVol / Math.Max(1, noTumors) : 0.0;

                // min inter-tumor surface distance (cm)
                double minDist_cm = 0.0; bool minDistSet = false;
                for (int i = 0; i < centers.Count; i++)
                {
                    for (int j = i + 1; j < centers.Count; j++)
                    {
                        double dx = centers[i].x - centers[j].x;
                        double dy = centers[i].y - centers[j].y;
                        double dz = centers[i].z - centers[j].z;
                        double d_mm = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        double surf_mm = d_mm - radii_mm[i] - radii_mm[j];
                        if (surf_mm < 0.0) surf_mm = 0.0;
                        double cm = surf_mm / 10.0;
                        if (!minDistSet || cm < minDist_cm) { minDist_cm = cm; minDistSet = true; }
                    }
                }

                double rxGy = TryGetRxGy(context) ?? 25.0;

                string refId = FindPreferredReferenceId(context.StructureSet);
                double refVol = (!string.IsNullOrEmpty(refId)) ? GetStructureVolumeCc(context.StructureSet, refId) : 0.0;

                double[] feat7 = new double[]
                {
                    (double)noTumors,
                    rxGy,
                    totalVol,
                    avgTumorVolCc,
                    avgSa_cm2,
                    avgEq_cm2,
                    minDist_cm
                };

                // cluster assignment
                int cid = ClusterAssigner3D.AssignCluster(noTumors, totalVol, avgSa_cm2, avgEq_cm2);

                // ---- Predictions ----

                // UNCLUSTERED Individual (3 singles) -> Ma,m
                double[] u_ind_vals = { double.NaN, double.NaN, double.NaN };
                double[] u_ind_sig = { double.NaN, double.NaN, double.NaN };

                try
                {
                    string[] f = { Paths.JSON_V50, Paths.JSON_V60, Paths.JSON_V667 };
                    for (int k = 0; k < 3; k++)
                    {
                        if (!File.Exists(f[k])) continue;
                        string js = File.ReadAllText(f[k]);
                        double yy, ss;
                        if (TryPredictSingle_SO(js, feat7, out yy, out ss))
                        {
                            // Apply empirical correction (CS → ML)
                            if (k == 0) EmpiricalCorrection.CorrectV50(ref yy, ref ss);
                            else if (k == 1) EmpiricalCorrection.CorrectV60(ref yy, ref ss);
                            else EmpiricalCorrection.CorrectV667(ref yy, ref ss);

                            // Physical clamps
                            if (yy < 0) yy = 0;
                            if (refVol > 0 && yy > refVol) yy = refVol;

                            u_ind_vals[k] = yy;
                            u_ind_sig[k] = ss;
                        }
                    }
                }
                catch { }

                // UNCLUSTERED 3-in-1 -> Ma,s
                double[] u_31_vals = { double.NaN, double.NaN, double.NaN };
                double[] u_31_sig = { double.NaN, double.NaN, double.NaN };

                try
                {
                    if (File.Exists(Paths.SHARED_3IN1))
                    {
                        string json = File.ReadAllText(Paths.SHARED_3IN1);
                        SimpleShared3OutModel m;
                        string perr;
                        if (SimpleShared3OutModel.TryParse(json, out m, out perr))
                        {
                            double[] X = SimpleShared3OutModel.MapFeaturesByName(
                                m.FeatureNames, noTumors, rxGy, totalVol,
                                avgTumorVolCc, avgSa_cm2, avgEq_cm2, minDist_cm);

                            double[] sig;
                            double[] y = SimpleShared3OutModel.PredictWithSigma(m, X, out sig);

                            // APPLY LINEAR %DIFF CORRECTION (CS → ML)
                            double v50 = y[0], s50 = sig[0];
                            double v60 = y[1], s60 = sig[1];
                            double v67 = y[2], s67 = sig[2];

                            U31LinearCorrection.CorrectV50(ref v50, ref s50);
                            U31LinearCorrection.CorrectV60(ref v60, ref s60);
                            U31LinearCorrection.CorrectV667(ref v67, ref s67);

                            // physical clamps
                            if (v50 < 0) v50 = 0; if (v60 < 0) v60 = 0; if (v67 < 0) v67 = 0;
                            if (refVol > 0)
                            {
                                if (v50 > refVol) v50 = refVol;
                                if (v60 > refVol) v60 = refVol;
                                if (v67 > refVol) v67 = refVol;
                            }

                            u_31_vals[0] = v50; u_31_sig[0] = s50;
                            u_31_vals[1] = v60; u_31_sig[1] = s60;
                            u_31_vals[2] = v67; u_31_sig[2] = s67;
                        }
                        else
                        {
                            MessageBox.Show("Failed to parse shared 3-in-1 model:\n" + perr);
                        }
                    }
                }
                catch { }

                // CLUSTERED Individual -> Mc,m  (UNCORRECTED now)
                double[] c_ind_vals = { double.NaN, double.NaN, double.NaN };
                double[] c_ind_sig = { double.NaN, double.NaN, double.NaN };

                try
                {
                    string cJson = (cid == 1) ? Paths.C1_INDIV : (cid == 2 ? Paths.C2_INDIV : Paths.C3_INDIV);
                    if (File.Exists(cJson))
                    {
                        string js = File.ReadAllText(cJson);
                        double[] y, s;
                        if (TryPredictC31(js, feat7, refVol, out y, out s))
                        {
                            // Use RAW predictions (no empirical correction)
                            double v50 = y[0], s50 = s[0];
                            double v60 = y[1], s60 = s[1];
                            double v67 = y[2], s67 = s[2];

                            // Physical clamps only
                            if (v50 < 0) v50 = 0; if (v60 < 0) v60 = 0; if (v67 < 0) v67 = 0;
                            if (refVol > 0)
                            {
                                if (v50 > refVol) v50 = refVol;
                                if (v60 > refVol) v60 = refVol;
                                if (v67 > refVol) v67 = refVol;
                            }

                            c_ind_vals[0] = v50; c_ind_sig[0] = s50;
                            c_ind_vals[1] = v60; c_ind_sig[1] = s60;
                            c_ind_vals[2] = v67; c_ind_sig[2] = s67;
                        }
                    }
                }
                catch { }

                // CLUSTERED 3-in-1 (mini logic) -> Mc,s   (already uncorrected)
                double[] c_31_vals = { double.NaN, double.NaN, double.NaN };
                double[] c_31_sig = { double.NaN, double.NaN, double.NaN };

                try
                {
                    string cJson = (cid == 1) ? Paths.C1_3IN1 : (cid == 2 ? Paths.C2_3IN1 : Paths.C3_3IN1);
                    if (File.Exists(cJson))
                    {
                        string js = File.ReadAllText(cJson);

                        List<string>[] perOutputNames = null; // use FeatureNames from JSON
                        double[] yMini, sMini;
                        if (TryPredictC31_Mini(js, perOutputNames, feat7, refVol, out yMini, out sMini))
                        {
                            c_31_vals[0] = yMini[0]; c_31_sig[0] = sMini[0];
                            c_31_vals[1] = yMini[1]; c_31_sig[1] = sMini[1];
                            c_31_vals[2] = yMini[2]; c_31_sig[2] = sMini[2];
                        }
                        else
                        {
                            MessageBox.Show("Failed to parse clustered 3-in-1 model for C" + cid + " (mini logic).");
                        }
                    }
                }
                catch { }

                // ---- Global minima for highlighting ----
                bool[] h_Mas = new bool[3]; // Ma,s = u_31
                bool[] h_Mam = new bool[3]; // Ma,m = u_ind
                bool[] h_Mcs = new bool[3]; // Mc,s = c_31
                bool[] h_Mcm = new bool[3]; // Mc,m = c_ind

                for (int i = 0; i < 3; i++)
                {
                    double min = double.PositiveInfinity;
                    double[] cand = { u_31_vals[i], u_ind_vals[i], c_31_vals[i], c_ind_vals[i] };
                    foreach (var v in cand) if (!double.IsNaN(v) && v < min) min = v;

                    h_Mas[i] = (!double.IsNaN(u_31_vals[i]) && Math.Abs(u_31_vals[i] - min) < 1e-12);
                    h_Mam[i] = (!double.IsNaN(u_ind_vals[i]) && Math.Abs(u_ind_vals[i] - min) < 1e-12);
                    h_Mcs[i] = (!double.IsNaN(c_31_vals[i]) && Math.Abs(c_31_vals[i] - min) < 1e-12);
                    h_Mcm[i] = (!double.IsNaN(c_ind_vals[i]) && Math.Abs(c_ind_vals[i] - min) < 1e-12);
                }

                // ---------- UI ----------
                var win = new Window();
                win.Title = "SIMT SRS GBRT Brain Toxicity Prediction (cc)";
                win.SizeToContent = SizeToContent.WidthAndHeight;
                win.ResizeMode = ResizeMode.NoResize;
                win.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                var rootPanel = new StackPanel { Margin = new Thickness(12) };

                // Title
                rootPanel.Children.Add(new TextBlock
                {
                    Text = "SIMT SRS GBRT Brain Toxicity Prediction",
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 6),
                    TextAlignment = TextAlignment.Left
                });

                // Table Grid: 4 columns
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // Model name
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // Endpoints
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // Prediction results
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });   // Plot

                // Header row
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddHeaderCell(grid, 0, 0, "Model name");
                AddHeaderCell(grid, 0, 1, "Endpoints (V50%, V60%, V66.7%)");
                AddHeaderCell(grid, 0, 2, "Prediction results (cc)");
                AddHeaderCell(grid, 0, 3, "Plot of prediction points");

                // Model rows (fixed order): Ma,s ; Ma,m ; Mc,s ; Mc,m
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ModelRowHeight) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ModelRowHeight) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ModelRowHeight) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ModelRowHeight) });

                int rowBase = 1;
                AddModelRow(grid, rowBase + 0, "a,s", false, cid, u_31_vals, u_31_sig, h_Mas);
                AddModelRow(grid, rowBase + 1, "a,m", false, cid, u_ind_vals, u_ind_sig, h_Mam);
                AddModelRow(grid, rowBase + 2, "c,s", true, cid, c_31_vals, c_31_sig, h_Mcs);
                AddModelRow(grid, rowBase + 3, "c,m", true, cid, c_ind_vals, c_ind_sig, h_Mcm);

                rootPanel.Children.Add(grid);

                // 3D images section
                var figPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
                figPanel.Children.Add(new TextBlock
                {
                    Text = "3D Plot of Clustering (Training Set)",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                // Image 1
                try
                {
                    if (File.Exists(Paths.IMG_THREE_D_PLOT))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(Paths.IMG_THREE_D_PLOT, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();

                        var img = new System.Windows.Controls.Image
                        {
                            Source = bmp,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Stretch = Stretch.Uniform,
                            MaxHeight = 260
                        };
                        figPanel.Children.Add(img);
                    }
                }
                catch { }

                // Image 2 (5× smaller)
                try
                {
                    if (File.Exists(Paths.IMG_THREE_D_BAR))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(Paths.IMG_THREE_D_BAR, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();

                        var img = new System.Windows.Controls.Image
                        {
                            Source = bmp,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Stretch = Stretch.Uniform,
                            MaxHeight = 52 // 260/5
                        };
                        figPanel.Children.Add(img);
                    }
                }
                catch { }

                rootPanel.Children.Add(figPanel);

                win.Content = rootPanel;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
        }

        private static void AddHeaderCell(Grid grid, int row, int col, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 2, 4, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        private static void AddModelRow(Grid grid, int row, string subscript, bool showClusterBadge, int cid,
                                        double[] vals, double[] sig, bool[] highlight)
        {
            // Col 0: model name (+ cluster badge if clustered)
            var cellPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            cellPanel.Children.Add(MakeModelLabel(subscript, SystemFonts.MessageFontSize));
            if (showClusterBadge) cellPanel.Children.Add(MakeClusterBadge(cid));
            Grid.SetRow(cellPanel, row); Grid.SetColumn(cellPanel, 0); grid.Children.Add(cellPanel);

            // Col 1: endpoints
            var ep = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ep.Children.Add(new TextBlock { Text = "V50%", Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center });
            ep.Children.Add(new TextBlock { Text = "V60%", Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center });
            ep.Children.Add(new TextBlock { Text = "V66.7%", Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetRow(ep, row); Grid.SetColumn(ep, 1); grid.Children.Add(ep);

            // Col 2: results
            var rp = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < 3; i++)
            {
                var t = ValCellNA(vals[i], sig != null && sig.Length > i ? sig[i] : double.NaN, highlight != null && highlight.Length > i ? highlight[i] : false);
                rp.Children.Add(t);
            }
            Grid.SetRow(rp, row); Grid.SetColumn(rp, 2); grid.Children.Add(rp);

            // Col 3: mini plot with bounds = 0.9×min .. 1.1×max
            double mn = double.PositiveInfinity, mx = double.NegativeInfinity;
            for (int i = 0; i < 3; i++)
            {
                if (!double.IsNaN(vals[i])) { if (vals[i] < mn) mn = vals[i]; if (vals[i] > mx) mx = vals[i]; }
            }
            bool invalidMn = double.IsNaN(mn) || double.IsInfinity(mn);
            bool invalidMx = double.IsNaN(mx) || double.IsInfinity(mx);
            if (invalidMn) { mn = 0.0; }
            if (invalidMx) { mx = 1.0; }
            var mp = new MiniPlot
            {
                Values = new[] { vals[0], vals[1], vals[2] },
                Sigmas = new[] { sig != null && sig.Length > 0 ? sig[0] : 0.0,
                                 sig != null && sig.Length > 1 ? sig[1] : 0.0,
                                 sig != null && sig.Length > 2 ? sig[2] : 0.0 },
                Width = MiniPlotWidth,
                Height = MiniPlotHeight,
                YMinBound = Math.Max(0.0, 0.9 * mn),
                YMaxBound = Math.Max(0.9 * mn + 1e-6, 1.1 * mx)
            };
            Grid.SetRow(mp, row); Grid.SetColumn(mp, 3); grid.Children.Add(mp);
        }
    }

    // ---------- TOP-LEVEL EXTENSION (adds tree to model list) ----------
    internal static class MiniExt
    {
        public static void AddTreeToLatest(
            this List<Script.MiniSingleOutputModel> outs,
            Script.MiniSingleOutputModel m,
            Script.MiniTreeSO t)
        {
            if (m != null && t != null)
            {
                m.Trees.Add(t);
            }
        }
    }

    // ======================================================================
    // ====== EXACT simplified unclustered 3-in-1 model (with per-σ) ========
    // ======================================================================

    internal class SimpleShared3OutModel
    {
        public double LearningRate = 0.1;
        public double[] PerOutputLearningRate = null;
        public double[] InitialPrediction = new double[] { 0.0, 0.0, 0.0 };
        public List<string> FeatureNames = new List<string>();
        public List<List<Node>> Trees = new List<List<Node>>();
        public bool GoLeftIfLE = true;
        public double ComparatorEps = 0.0;

        internal class Node
        {
            public bool IsLeaf;
            public int Feature;
            public double Threshold;
            public int LeftIndex;
            public int RightIndex;
            public double[] MultiValue;
            public double[] MultiStd;
            public int NodeId;
            public int LeftChildId;
            public int RightChildId;
        }

        public static double[] PredictWithSigma(SimpleShared3OutModel m, double[] X, out double[] sigma)
        {
            double[] y = new double[3];
            double[] var = new double[3];
            y[0] = (m.InitialPrediction != null && m.InitialPrediction.Length > 0) ? m.InitialPrediction[0] : 0.0;
            y[1] = (m.InitialPrediction != null && m.InitialPrediction.Length > 1) ? m.InitialPrediction[1] : 0.0;
            y[2] = (m.InitialPrediction != null && m.InitialPrediction.Length > 2) ? m.InitialPrediction[2] : 0.0;

            double lr_g = (m.LearningRate > 0) ? m.LearningRate : 0.1;
            double l0 = (m.PerOutputLearningRate != null && m.PerOutputLearningRate.Length >= 3) ? m.PerOutputLearningRate[0] : lr_g;
            double l1 = (m.PerOutputLearningRate != null && m.PerOutputLearningRate.Length >= 3) ? m.PerOutputLearningRate[1] : lr_g;
            double l2 = (m.PerOutputLearningRate != null && m.PerOutputLearningRate.Length >= 3) ? m.PerOutputLearningRate[2] : lr_g;

            for (int t = 0; t < m.Trees.Count; t++)
            {
                List<Node> nodes = m.Trees[t];
                if (nodes == null || nodes.Count == 0) continue;

                bool needsMap = false;
                for (int k = 0; k < nodes.Count; k++)
                {
                    if (nodes[k].LeftIndex == int.MinValue || nodes[k].RightIndex == int.MinValue)
                    {
                        needsMap = true; break;
                    }
                }
                Dictionary<int, int> id2idx = null;
                if (needsMap)
                {
                    id2idx = new Dictionary<int, int>();
                    for (int k = 0; k < nodes.Count; k++)
                    {
                        if (!id2idx.ContainsKey(nodes[k].NodeId)) id2idx[nodes[k].NodeId] = k;
                    }
                }

                int idx = 0; // assume node 0 is root
                int guard = 0;
                while (idx >= 0 && idx < nodes.Count && guard++ < 4096)
                {
                    Node n = nodes[idx];
                    if (n.IsLeaf)
                    {
                        double mv0 = (n.MultiValue != null && n.MultiValue.Length >= 3) ? n.MultiValue[0] : 0.0;
                        double mv1 = (n.MultiValue != null && n.MultiValue.Length >= 3) ? n.MultiValue[1] : 0.0;
                        double mv2 = (n.MultiValue != null && n.MultiValue.Length >= 3) ? n.MultiValue[2] : 0.0;

                        double sd0 = (n.MultiStd != null && n.MultiStd.Length >= 3) ? Math.Max(0.0, n.MultiStd[0]) : 0.0;
                        double sd1 = (n.MultiStd != null && n.MultiStd.Length >= 3) ? Math.Max(0.0, n.MultiStd[1]) : 0.0;
                        double sd2 = (n.MultiStd != null && n.MultiStd.Length >= 3) ? Math.Max(0.0, n.MultiStd[2]) : 0.0;

                        y[0] += l0 * mv0; var[0] += (l0 * l0) * (sd0 * sd0);
                        y[1] += l1 * mv1; var[1] += (l1 * l1) * (sd1 * sd1);
                        y[2] += l2 * mv2; var[2] += (l2 * l2) * (sd2 * sd2);
                        break;
                    }

                    double xv = (n.Feature >= 0 && n.Feature < X.Length) ? X[n.Feature] : 0.0;
                    bool goLeft = m.GoLeftIfLE ? (xv <= n.Threshold + m.ComparatorEps) : (xv < n.Threshold - m.ComparatorEps);

                    int nextIdx;
                    if (n.LeftIndex != int.MinValue && n.RightIndex != int.MinValue)
                    {
                        nextIdx = goLeft ? n.LeftIndex : n.RightIndex;
                    }
                    else if (id2idx != null)
                    {
                        int childId = goLeft ? n.LeftChildId : n.RightChildId;
                        nextIdx = (childId != 0 && id2idx.ContainsKey(childId)) ? id2idx[childId] : -1;
                    }
                    else
                    {
                        break;
                    }

                    if (nextIdx < 0 || nextIdx >= nodes.Count) break;
                    idx = nextIdx;
                }
            }

            sigma = new[] { Math.Sqrt(Math.Max(0.0, var[0])), Math.Sqrt(Math.Max(0.0, var[1])), Math.Sqrt(Math.Max(0.0, var[2])) };
            for (int k = 0; k < 3; k++) if (y[k] < 0) y[k] = 0;
            return y;
        }

        public static double[] Predict(SimpleShared3OutModel m, double[] X)
        {
            double[] _; return PredictWithSigma(m, X, out _);
        }

        public static double[] MapFeaturesByName(
            List<string> names,
            int noTumors, double totalDoseGy, double totalVolCc,
            double avgTumVolCc, double avgSa_cm2, double avgEq_cm2, double minDist_cm)
        {
            if (names == null) names = new List<string>();
            double[] X = new double[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string key = names[i] == null ? "" : names[i].Trim();
                double v = 0.0;

                if (string.Equals(key, "NoTumors", StringComparison.OrdinalIgnoreCase)) v = noTumors;
                else if (string.Equals(key, "TotalDose", StringComparison.OrdinalIgnoreCase)) v = totalDoseGy;
                else if (string.Equals(key, "TotalVolume", StringComparison.OrdinalIgnoreCase)) v = totalVolCc;
                else if (string.Equals(key, "AvgTumorVolume", StringComparison.OrdinalIgnoreCase)) v = avgTumVolCc;
                else if (string.Equals(key, "AvgSurfaceArea", StringComparison.OrdinalIgnoreCase)) v = avgSa_cm2;
                else if (string.Equals(key, "AvgEquivSA", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "AvgEqSphereSA", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "AvgEqSA", StringComparison.OrdinalIgnoreCase)) v = avgEq_cm2;
                else if (string.Equals(key, "MinShortestDistance", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "MinInterTumorDistance", StringComparison.OrdinalIgnoreCase)) v = minDist_cm;
                else v = 0.0;

                X[i] = v;
            }
            return X;
        }

        public static bool TryParse(string json, out SimpleShared3OutModel model, out string error)
        {
            model = new SimpleShared3OutModel();
            error = null;
            try
            {
                string s = (json ?? "").Replace("\r\n", "\n");

                model.GoLeftIfLE = J3.ExtractBoolCI(s, new[] { "\"GoLeftIfLessOrEqual\"", "'GoLeftIfLessOrEqual'", "\"SplitGoLeftIfLessOrEqual\"", "'SplitGoLeftIfLessOrEqual'" }, true);
                model.ComparatorEps = J3.ExtractDoubleCI(s, new[] { "\"ComparatorEpsilon\"", "'ComparatorEpsilon'" }, 0.0);

                int oe = J3.IndexAfterCI(s, new[] { "\"OutputEnsembles\"", "'OutputEnsembles'" });
                string ensembleObj = null;
                if (oe >= 0)
                {
                    string block = J3.ExtractBlock(s, oe, '[', ']');
                    string inner = J3.TrimOuter(block, '[', ']');

                    int i = 0, depth = 0, start = -1;
                    while (i < inner.Length)
                    {
                        char c = inner[i];
                        if (c == '{') { if (depth == 0) start = i; depth++; }
                        else if (c == '}')
                        {
                            depth--;
                            if (depth == 0 && start >= 0)
                            {
                                ensembleObj = inner.Substring(start, i - start + 1);
                                break;
                            }
                        }
                        else if (c == '"')
                        {
                            i++;
                            while (i < inner.Length)
                            {
                                if (inner[i] == '"' && inner[i - 1] != '\\') break;
                                i++;
                            }
                        }
                        i++;
                    }
                }
                if (ensembleObj == null) ensembleObj = s;

                model.LearningRate = J3.ExtractDoubleCI(ensembleObj, new[] { "\"LearningRate\"", "'LearningRate'" }, 0.1);

                double[] perLR = J3.ExtractDoubleArrayCI(ensembleObj, new[] { "\"PerOutputLearningRate\"", "'PerOutputLearningRate'" });
                if (perLR != null && perLR.Length >= 3) model.PerOutputLearningRate = new[] { perLR[0], perLR[1], perLR[2] };

                double[] init = J3.ExtractDoubleArrayCI(ensembleObj, new[] { "\"InitialPrediction\"", "'InitialPrediction'" });
                if (init != null && init.Length >= 3)
                    model.InitialPrediction = new double[] { init[0], init[1], init[2] };

                var feats = J3.ExtractStringArrayCI(ensembleObj, new[] { "\"FeatureNames\"", "'FeatureNames'" });
                if (feats != null) model.FeatureNames = feats;

                int tIdx = J3.IndexAfterCI(ensembleObj, new[] { "\"Trees\"", "'Trees'" });
                string tBlock = (tIdx >= 0) ? J3.ExtractBlock(ensembleObj, tIdx, '[', ']') : "[]";
                string tInner = J3.TrimOuter(tBlock, '[', ']');

                var treeArrays = new List<string>();
                {
                    int i = 0, depth = 0, start = -1;
                    while (i < tInner.Length)
                    {
                        char c = tInner[i];
                        if (c == '[') { if (depth == 0) start = i; depth++; }
                        else if (c == ']')
                        {
                            depth--;
                            if (depth == 0 && start >= 0)
                            {
                                treeArrays.Add(tInner.Substring(start, i - start + 1));
                                start = -1;
                            }
                        }
                        else if (c == '"')
                        {
                            i++;
                            while (i < tInner.Length)
                            {
                                if (tInner[i] == '"' && tInner[i - 1] != '\\') break;
                                i++;
                            }
                        }
                        i++;
                    }
                }

                for (int ti = 0; ti < treeArrays.Count; ti++)
                {
                    string arr = treeArrays[ti];
                    string nodesTxt = J3.TrimOuter(arr, '[', ']');
                    var nodes = new List<Node>();

                    int i = 0, depth = 0, start = -1;
                    while (i < nodesTxt.Length)
                    {
                        char c = nodesTxt[i];
                        if (c == '{') { if (depth == 0) start = i; depth++; }
                        else if (c == '}')
                        {
                            depth--;
                            if (depth == 0 && start >= 0)
                            {
                                string nodeObj = nodesTxt.Substring(start, i - start + 1);
                                nodes.Add(ParseNode(nodeObj));
                                start = -1;
                            }
                        }
                        else if (c == '"')
                        {
                            i++;
                            while (i < nodesTxt.Length)
                            {
                                if (nodesTxt[i] == '"' && nodesTxt[i - 1] != '\\') break;
                                i++;
                            }
                        }
                        i++;
                    }

                    bool needMap = false;
                    for (int k = 0; k < nodes.Count; k++)
                    {
                        if (nodes[k].LeftIndex == int.MinValue || nodes[k].RightIndex == int.MinValue)
                        {
                            needMap = true; break;
                        }
                    }
                    if (needMap)
                    {
                        var id2idx = new Dictionary<int, int>();
                        for (int k = 0; k < nodes.Count; k++)
                        {
                            int id = nodes[k].NodeId;
                            if (!id2idx.ContainsKey(id)) id2idx[id] = k;
                        }
                        for (int k = 0; k < nodes.Count; k++)
                        {
                            Node n = nodes[k];
                            if (n.LeftIndex == int.MinValue)
                                n.LeftIndex = (n.LeftChildId != 0 && id2idx.ContainsKey(n.LeftChildId)) ? id2idx[n.LeftChildId] : -1;
                            if (n.RightIndex == int.MinValue)
                                n.RightIndex = (n.RightChildId != 0 && id2idx.ContainsKey(n.RightChildId)) ? id2idx[n.RightChildId] : -1;
                        }
                    }

                    model.Trees.Add(nodes);
                }

                if (model.Trees.Count == 0) { error = "No Trees found."; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static SimpleShared3OutModel.Node ParseNode(string s)
        {
            var n = new SimpleShared3OutModel.Node();
            n.IsLeaf = J3.ExtractBoolCI(s, new[] { "\"IsLeaf\"", "'IsLeaf'" }, false);

            string splitTok = J3.ExtractStringCI(s, new[] { "\"SplitFeature\"", "'SplitFeature'" }, "");
            int f = MapSplitFeatureTokenToIndex(splitTok);
            if (f < 0)
            {
                int f1 = J3.ExtractIntCI(s, new[] { "\"FeatureIndex\"", "'FeatureIndex'" }, 0);
                f = (f1 > 0) ? (f1 - 1) : 0;
            }
            n.Feature = f;

            n.Threshold = J3.ExtractDoubleCI(s, new[] { "\"Threshold\"", "'Threshold'" }, 0.0);

            int li = J3.ExtractIntCI(s, new[] { "\"LeftIndex\"", "'LeftIndex'" }, int.MinValue);
            int ri = J3.ExtractIntCI(s, new[] { "\"RightIndex\"", "'RightIndex'" }, int.MinValue);
            if (li != int.MinValue && ri != int.MinValue)
            {
                n.LeftIndex = li;
                n.RightIndex = ri;
                n.NodeId = J3.ExtractIntCI(s, new[] { "\"NodeId\"", "'NodeId'" }, 0);
                n.LeftChildId = J3.ExtractIntCI(s, new[] { "\"LeftChild\"", "'LeftChild'" }, 0);
                n.RightChildId = J3.ExtractIntCI(s, new[] { "\"RightChild\"", "'RightChild'" }, 0);
            }
            else
            {
                n.NodeId = J3.ExtractIntCI(s, new[] { "\"NodeId\"", "'NodeId'" }, 0);
                n.LeftChildId = J3.ExtractIntCI(s, new[] { "\"LeftChild\"", "'LeftChild'" }, 0);
                n.RightChildId = J3.ExtractIntCI(s, new[] { "\"RightChild\"", "'RightChild'" }, 0);
                n.LeftIndex = int.MinValue;
                n.RightIndex = int.MinValue;
            }

            if (n.IsLeaf)
            {
                double[] mv = J3.ExtractDoubleArrayCI(s, new[] { "\"MultiValue\"", "'MultiValue'", "\"Value\"", "'Value'" });
                if (mv == null) mv = new double[0];
                if (mv.Length >= 3)
                    n.MultiValue = new double[] { mv[0], mv[1], mv[2] };
                else if (mv.Length == 1)
                    n.MultiValue = new double[] { mv[0], mv[0], mv[0] };
                else
                    n.MultiValue = new double[] { 0.0, 0.0, 0.0 };

                double[] rs = J3.ExtractDoubleArrayCI(s, new[] { "\"RegionStdPerOutput\"", "'RegionStdPerOutput'" });
                if (rs == null || rs.Length < 3)
                    rs = J3.ExtractDoubleArrayCI(s, new[] { "\"LeafStd\"", "'LeafStd'" });
                if (rs != null && rs.Length >= 3)
                    n.MultiStd = new[] { Math.Max(0.0, rs[0]), Math.Max(0.0, rs[1]), Math.Max(0.0, rs[2]) };
                else
                    n.MultiStd = new[] { 0.0, 0.0, 0.0 };
            }
            else
            {
                n.MultiValue = null;
                n.MultiStd = null;
            }

            return n;
        }

        private static int MapSplitFeatureTokenToIndex(string splitFeature)
        {
            if (string.IsNullOrEmpty(splitFeature)) return -1;
            string t = splitFeature.Trim();
            if (t.Length >= 2 && (t[0] == 'x' || t[0] == 'X'))
            {
                int k;
                if (int.TryParse(t.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && k >= 1)
                    return k - 1;
            }
            return -1;
        }
    }

    internal static class J3
    {
        public static int IndexAfterCI(string s, string[] keys)
        {
            if (s == null) return -1;
            for (int i = 0; i < keys.Length; i++)
            {
                int k = CultureInfo.InvariantCulture.CompareInfo.IndexOf(s, keys[i], CompareOptions.IgnoreCase);
                if (k >= 0)
                {
                    int colon = s.IndexOf(':', k + keys[i].Length);
                    if (colon >= 0) return colon + 1;
                }
            }
            return -1;
        }
        public static string ExtractBlock(string s, int startAtColon, char open, char close)
        {
            if (s == null || startAtColon < 0) return "";
            int pos = startAtColon;
            while (pos < s.Length && s[pos] != open) pos++;
            if (pos >= s.Length) return "";
            int depth = 0;
            for (int i = pos; i < s.Length; i++)
            {
                char cc = s[i];
                if (cc == open) depth++;
                else if (cc == close)
                {
                    depth--;
                    if (depth == 0) return s.Substring(pos, i - pos + 1);
                }
                else if (cc == '"')
                {
                    i++;
                    while (i < s.Length)
                    {
                        if (s[i] == '"' && s[i - 1] != '\\') break;
                        i++;
                    }
                }
            }
            return "";
        }
        public static string TrimOuter(string s, char open, char close)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s.Trim();
            if (t.Length >= 2 && t[0] == open && t[t.Length - 1] == close) return t.Substring(1, t.Length - 2);
            return t;
        }
        public static double ExtractDoubleCI(string s, string[] keys, double def)
        {
            int ai = IndexAfterCI(s, keys); if (ai < 0) return def;
            int i = ai;
            while (i < s.Length && !(char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+')) i++;
            if (i >= s.Length) return def;
            int j = i;
            while (j < s.Length)
            {
                char cc = s[j];
                if (char.IsDigit(cc) || cc == '-' || cc == '+' || cc == '.' || cc == 'E' || cc == 'e') j++;
                else break;
            }
            double v;
            return double.TryParse(s.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
        }
        public static int ExtractIntCI(string s, string[] keys, int def) { return (int)Math.Round(ExtractDoubleCI(s, keys, def)); }
        public static bool ExtractBoolCI(string s, string[] keys, bool def)
        {
            int ai = IndexAfterCI(s, keys); if (ai < 0) return def;
            int j = ai;
            while (j < s.Length && (char.IsWhiteSpace(s[j]) || s[j] == ',')) j++;
            int start = j;
            while (j < s.Length && s[j] != ',' && s[j] != '}' && s[j] != ']') j++;
            string tok = s.Substring(start, Math.Max(0, j - start)).Trim().Trim('"');
            if (string.Compare(tok, "true", true, CultureInfo.InvariantCulture) == 0) return true;
            if (string.Compare(tok, "false", true, CultureInfo.InvariantCulture) == 0) return false;
            return def;
        }
        public static double[] ExtractDoubleArrayCI(string s, string[] keys)
        {
            int ai = IndexAfterCI(s, keys); if (ai < 0) return null;
            string block = ExtractBlock(s, ai, '[', ']');
            string inner = TrimOuter(block, '[', ']');
            if (string.IsNullOrWhiteSpace(inner)) return new double[0];
            List<double> vals = new List<double>(); int i = 0;
            while (i < inner.Length)
            {
                while (i < inner.Length && (char.IsWhiteSpace(inner[i]) || inner[i] == ',')) i++;
                if (i >= inner.Length) break;
                int s0 = i;
                while (i < inner.Length)
                {
                    char cc = inner[i];
                    if (char.IsDigit(cc) || cc == '-' || cc == '+' || cc == '.' || cc == 'E' || cc == 'e') i++;
                    else break;
                }
                double v;
                if (double.TryParse(inner.Substring(s0, i - s0), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) vals.Add(v);
            }
            return vals.ToArray();
        }
        public static List<string> ExtractStringArrayCI(string s, string[] keys)
        {
            int ai = IndexAfterCI(s, keys); if (ai < 0) return null;
            string block = ExtractBlock(s, ai, '[', ']'); string inner = TrimOuter(block, '[', ']');
            List<string> res = new List<string>(); int i = 0;
            while (i < inner.Length)
            {
                while (i < inner.Length && (char.IsWhiteSpace(inner[i]) || inner[i] == ',')) i++;
                if (i >= inner.Length) break;
                if (inner[i] == '"')
                {
                    int start = i + 1; int j = start; while (j < inner.Length) { if (j < inner.Length && inner[j] == '"' && inner[j - 1] != '\\') break; j++; }
                    if (j >= inner.Length) break; res.Add(inner.Substring(start, j - start)); i = j + 1;
                }
                else
                {
                    int s0 = i; while (i < inner.Length && inner[i] != ',' && inner[i] != ']') i++;
                    string tok = inner.Substring(s0, i - s0).Trim().Trim('"'); if (tok.Length > 0) res.Add(tok);
                }
            }
            return res;
        }
        public static string ExtractStringCI(string s, string[] keys, string def = "")
        {
            int ai = IndexAfterCI(s, keys); if (ai < 0) return def;
            int j = ai;
            while (j < s.Length && (char.IsWhiteSpace(s[j]) || s[j] == ',')) j++;
            if (j >= s.Length) return def;
            if (s[j] == '"')
            {
                int start = j + 1; int i = start;
                while (i < s.Length)
                {
                    if (i == s.Length) break;
                    if (s[i] == '"' && i > start && s[i - 1] != '\\') break;
                    i++;
                }
                if (i < s.Length) return s.Substring(start, i - start);
            }
            return def;
        }
    }
}
#pragma warning restore 0429
