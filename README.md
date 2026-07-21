# SRS Target Information

An ESAPI tool for automatically extracting geometric characteristics of target lesions from Varian Eclipse treatment plans.

The script identifies all non-empty structures whose IDs begin with **"Exp"** and summarizes their geometric information in an interactive WPF interface. It was developed to support research involving single-isocenter multi-target stereotactic radiosurgery (SIMT SRS).

---

## Features

- Automatically detects all non-empty **Exp*** target structures
- Computes:
  - Volume (cc)
  - Bounding-box maximum dimension (mm)
  - Equivalent-sphere diameter (mm)
  - Diameter ratio (equivalent-sphere diameter / bounding-box maximum dimension)
- Interactive WPF graphical interface
- Sortable and resizable results table
- Automatic validation of missing or invalid structures
- Compatible with Varian Eclipse ESAPI

---

## Repository Structure

```
SRS-Target-Information/
│
├── GUI_SRS_Target_Information.cs
├── README.md
├── LICENSE
└── .gitignore
```

---

## Requirements

- Varian Eclipse
- Eclipse Scripting API (ESAPI)
- .NET Framework 4.5 or later
- Microsoft Visual Studio

Required ESAPI assemblies (not included):

- `VMS.TPS.Common.Model.API.dll`
- `VMS.TPS.Common.Model.Types.dll`

These DLLs must be referenced from your local Eclipse installation.

---

## Installation

1. Clone the repository.

```bash
git clone https://github.com/Zhuoyun-Huang/SRS-Target-Information.git
```

2. Open the project in Visual Studio.

3. Reference the ESAPI assemblies from your Eclipse installation.

4. Build the project.

5. Launch the script from Eclipse.

---

## Workflow

1. Open a patient in Eclipse.
2. Load a valid Structure Set.
3. Run the script.
4. All non-empty structures whose IDs begin with **"Exp"** are automatically identified.
5. Results are displayed in an interactive table containing:

- Structure ID
- Volume (cc)
- Bounding-box maximum dimension (mm)
- Equivalent-sphere diameter (mm)
- Diameter ratio

---

## Method

### Equivalent-Sphere Diameter

The equivalent-sphere diameter is calculated from the lesion volume by assuming a sphere with identical volume:

\[
D = 2\left(\frac{3V}{4\pi}\right)^{1/3}
\]

where \(V\) is the lesion volume.

### Bounding-Box Maximum Dimension

The maximum lesion dimension is estimated using the axis-aligned mesh bounding box provided by Eclipse.

> **Note:** This is **not** the true maximum Euclidean distance across the lesion.

### Diameter Ratio

The diameter ratio is defined as

```
min(Equivalent Sphere Diameter,
    Bounding-Box Maximum Dimension)
────────────────────────────────────
max(Equivalent Sphere Diameter,
    Bounding-Box Maximum Dimension)
```

Values closer to **1** indicate lesions whose equivalent sphere diameter is similar to the bounding-box maximum dimension, while smaller values indicate increasingly elongated or irregular geometries.

> **Important:** This diameter ratio is **not** the conventional three-dimensional sphericity metric.

---

## Example Output

| Structure ID | Volume (cc) | Bounding-box Max Dimension (mm) | Equivalent-Sphere Diameter (mm) | Diameter Ratio |
|--------------|------------:|--------------------------------:|--------------------------------:|---------------:|
| Exp01 | 1.82 | 18.6 | 15.1 | 0.81 |
| Exp02 | 0.94 | 14.3 | 12.2 | 0.85 |

---

## Disclaimer

This software is intended for **research and educational purposes only**.

It has **not** been validated for clinical use and should **not** be used as the sole basis for clinical decision-making. Users are responsible for independently verifying all measurements before any clinical application.

---

## License

This project is released under the **MIT License**.

---

## Author

**Zhuoyun Huang**

PhD Student, Medical Physics  
University at Buffalo

- GitHub: https://github.com/Zhuoyun-Huang
- LinkedIn: https://www.linkedin.com/in/zhuoyun-huang-0b55533bb/
