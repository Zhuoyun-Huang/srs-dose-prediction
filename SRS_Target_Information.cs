// GUI_SRS_Target_Information.cs
// ESAPI C# (.NET Framework 4.5+) single-file script
//
// Displays geometric information for all non-empty structures whose IDs
// begin with "Exp":
//   - Structure name
//   - Volume (cc)
//   - Bounding-box maximum dimension (mm)
//   - Equivalent-sphere diameter (mm)
//   - Equivalent-sphere diameter / bounding-box maximum-dimension ratio
//
// Notes:
//   - The maximum dimension is calculated from the axis-aligned mesh
//     bounding box. It is not the true maximum distance between arbitrary
//     surface points.
//   - The diameter ratio is a simplified geometric shape metric and is not
//     the conventional three-dimensional sphericity formula.
//   - Research and educational use only.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMS.TPS
{
    public class Script
    {
        private sealed class LesionRow
        {
            public string StructureId { get; set; }
            public string VolumeCc { get; set; }
            public string BoundingBoxMaxDimensionMm { get; set; }
            public string EquivalentSphereDiameterMm { get; set; }
            public string DiameterRatio { get; set; }
        }

        public void Execute(ScriptContext context)
        {
            try
            {
                if (context == null)
                {
                    MessageBox.Show(
                        "No Eclipse script context is available.",
                        "SRS Target Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (context.Patient == null)
                {
                    MessageBox.Show(
                        "Open a patient before running this script.",
                        "SRS Target Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (context.StructureSet == null)
                {
                    MessageBox.Show(
                        "Open a valid structure set before running this script.",
                        "SRS Target Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                var expStructures = context.StructureSet.Structures
                    .Where(
                        structure =>
                            structure != null &&
                            !structure.IsEmpty &&
                            !string.IsNullOrWhiteSpace(structure.Id) &&
                            structure.Id.StartsWith(
                                "Exp",
                                StringComparison.OrdinalIgnoreCase))
                    .OrderBy(
                        structure => structure.Id,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (expStructures.Count == 0)
                {
                    MessageBox.Show(
                        "No non-empty structures were found whose IDs begin with \"Exp\".",
                        "SRS Target Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var rows = new List<LesionRow>();

                foreach (var structure in expStructures)
                {
                    double volumeCc = GetValidVolumeCc(structure);

                    double equivalentSphereDiameterMm =
                        CalculateEquivalentSphereDiameterMm(volumeCc);

                    double boundingBoxMaxDimensionMm =
                        GetBoundingBoxMaximumDimensionMm(structure);

                    double diameterRatio = CalculateDiameterRatio(
                        equivalentSphereDiameterMm,
                        boundingBoxMaxDimensionMm);

                    rows.Add(
                        new LesionRow
                        {
                            StructureId = structure.Id,

                            VolumeCc = FormatNumber(
                                volumeCc,
                                "0.00"),

                            BoundingBoxMaxDimensionMm =
                                FormatNumberOrNA(
                                    boundingBoxMaxDimensionMm,
                                    "0.00"),

                            EquivalentSphereDiameterMm =
                                FormatNumberOrNA(
                                    equivalentSphereDiameterMm,
                                    "0.00"),

                            DiameterRatio =
                                FormatNumberOrNA(
                                    diameterRatio,
                                    "0.000")
                        });
                }

                ShowResultsWindow(rows);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "An unexpected error occurred:\n\n" + exception,
                    "SRS Target Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static double GetValidVolumeCc(Structure structure)
        {
            try
            {
                if (structure == null || structure.IsEmpty)
                {
                    return double.NaN;
                }

                double volumeCc = structure.Volume;

                if (double.IsNaN(volumeCc) ||
                    double.IsInfinity(volumeCc) ||
                    volumeCc <= 0.0)
                {
                    return double.NaN;
                }

                return volumeCc;
            }
            catch
            {
                return double.NaN;
            }
        }

        private static double CalculateEquivalentSphereDiameterMm(
            double volumeCc)
        {
            if (double.IsNaN(volumeCc) ||
                double.IsInfinity(volumeCc) ||
                volumeCc <= 0.0)
            {
                return double.NaN;
            }

            // 1 cc = 1000 mm^3
            double volumeMm3 = volumeCc * 1000.0;

            // Sphere:
            // V = (4/3)πr^3
            // r = [3V/(4π)]^(1/3)
            double radiusMm = Math.Pow(
                (3.0 * volumeMm3) / (4.0 * Math.PI),
                1.0 / 3.0);

            double diameterMm = 2.0 * radiusMm;

            if (double.IsNaN(diameterMm) ||
                double.IsInfinity(diameterMm) ||
                diameterMm <= 0.0)
            {
                return double.NaN;
            }

            return diameterMm;
        }

        private static double GetBoundingBoxMaximumDimensionMm(
            Structure structure)
        {
            try
            {
                if (structure == null ||
                    structure.IsEmpty ||
                    structure.MeshGeometry == null)
                {
                    return double.NaN;
                }

                Rect3D bounds = structure.MeshGeometry.Bounds;

                if (bounds.IsEmpty)
                {
                    return double.NaN;
                }

                double sizeX = bounds.SizeX;
                double sizeY = bounds.SizeY;
                double sizeZ = bounds.SizeZ;

                if (!IsPositiveFinite(sizeX) ||
                    !IsPositiveFinite(sizeY) ||
                    !IsPositiveFinite(sizeZ))
                {
                    return double.NaN;
                }

                return Math.Max(
                    sizeX,
                    Math.Max(sizeY, sizeZ));
            }
            catch
            {
                return double.NaN;
            }
        }

        private static double CalculateDiameterRatio(
            double equivalentSphereDiameterMm,
            double boundingBoxMaxDimensionMm)
        {
            if (!IsPositiveFinite(equivalentSphereDiameterMm) ||
                !IsPositiveFinite(boundingBoxMaxDimensionMm))
            {
                return double.NaN;
            }

            double smallerDiameter = Math.Min(
                equivalentSphereDiameterMm,
                boundingBoxMaxDimensionMm);

            double largerDiameter = Math.Max(
                equivalentSphereDiameterMm,
                boundingBoxMaxDimensionMm);

            if (largerDiameter <= 0.0)
            {
                return double.NaN;
            }

            return smallerDiameter / largerDiameter;
        }

        private static bool IsPositiveFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value) &&
                   value > 0.0;
        }

        private static string FormatNumber(
            double value,
            string format)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return "NA";
            }

            return value.ToString(
                format,
                CultureInfo.InvariantCulture);
        }

        private static string FormatNumberOrNA(
            double value,
            string format)
        {
            if (!IsPositiveFinite(value))
            {
                return "NA";
            }

            return value.ToString(
                format,
                CultureInfo.InvariantCulture);
        }

        private static void ShowResultsWindow(
            IList<LesionRow> rows)
        {
            var window = new Window
            {
                Title = "SRS Target Information",
                Width = 980,
                Height = 560,
                MinWidth = 800,
                MinHeight = 420,
                WindowStartupLocation =
                    WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var root = new DockPanel
            {
                Margin = new Thickness(12)
            };

            window.Content = root;

            var closeButton = new Button
            {
                Content = "Close",
                Width = 90,
                Height = 28,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsCancel = true
            };

            closeButton.Click +=
                delegate
                {
                    window.Close();
                };

            DockPanel.SetDock(
                closeButton,
                Dock.Bottom);

            root.Children.Add(closeButton);

            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 10)
            };

            DockPanel.SetDock(
                headerPanel,
                Dock.Top);

            root.Children.Add(headerPanel);

            var titleText = new TextBlock
            {
                Text = "Patient Lesion Information",
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 4)
            };

            headerPanel.Children.Add(titleText);

            var summaryText = new TextBlock
            {
                Text =
                    rows.Count.ToString(
                        CultureInfo.InvariantCulture) +
                    " non-empty Exp* structure(s) found.",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            };

            headerPanel.Children.Add(summaryText);

            var explanationText = new TextBlock
            {
                Text =
                    "The maximum dimension is obtained from the axis-aligned " +
                    "mesh bounding box. The final ratio is a simplified " +
                    "diameter-based shape metric, not conventional 3D sphericity.",
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 5)
            };

            headerPanel.Children.Add(explanationText);

            var disclaimerText = new TextBlock
            {
                Text =
                    "Research and educational use only. All measurements " +
                    "should be independently verified before clinical use.",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.DarkRed
            };

            headerPanel.Children.Add(disclaimerText);

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = true,
                CanUserResizeColumns = true,
                CanUserResizeRows = false,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HorizontalGridLinesBrush = Brushes.LightGray,
                VerticalGridLinesBrush = Brushes.LightGray,
                AlternatingRowBackground = Brushes.WhiteSmoke,
                RowBackground = Brushes.White,
                RowHeaderWidth = 0,
                Margin = new Thickness(0, 4, 0, 0),
                ItemsSource = rows
            };

            dataGrid.Columns.Add(
                CreateTextColumn(
                    "Structure ID",
                    "StructureId",
                    new DataGridLength(
                        1.0,
                        DataGridLengthUnitType.Star)));

            dataGrid.Columns.Add(
                CreateTextColumn(
                    "Volume (cc)",
                    "VolumeCc",
                    new DataGridLength(
                        110,
                        DataGridLengthUnitType.Pixel)));

            dataGrid.Columns.Add(
                CreateTextColumn(
                    "Bounding-box max dimension (mm)",
                    "BoundingBoxMaxDimensionMm",
                    new DataGridLength(
                        225,
                        DataGridLengthUnitType.Pixel)));

            dataGrid.Columns.Add(
                CreateTextColumn(
                    "Equivalent-sphere diameter (mm)",
                    "EquivalentSphereDiameterMm",
                    new DataGridLength(
                        225,
                        DataGridLengthUnitType.Pixel)));

            dataGrid.Columns.Add(
                CreateTextColumn(
                    "Diameter ratio",
                    "DiameterRatio",
                    new DataGridLength(
                        125,
                        DataGridLengthUnitType.Pixel)));

            root.Children.Add(dataGrid);

            window.ShowDialog();
        }

        private static DataGridTextColumn CreateTextColumn(
            string header,
            string propertyName,
            DataGridLength width)
        {
            var cellStyle = new Style(
                typeof(TextBlock));

            cellStyle.Setters.Add(
                new Setter(
                    TextBlock.TextAlignmentProperty,
                    TextAlignment.Center));

            cellStyle.Setters.Add(
                new Setter(
                    FrameworkElement.VerticalAlignmentProperty,
                    VerticalAlignment.Center));

            cellStyle.Setters.Add(
                new Setter(
                    FrameworkElement.MarginProperty,
                    new Thickness(4, 2, 4, 2)));

            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(propertyName),
                Width = width,
                ElementStyle = cellStyle
            };
        }
    }
}