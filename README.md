# SRS Target Information

ESAPI tool for automatically extracting geometric information from target lesions in Eclipse treatment plans.

This script identifies all non-empty structures whose IDs begin with **"Exp"** and summarizes their geometric characteristics in an interactive table. It is intended for research workflows involving single-isocenter multi-target stereotactic radiosurgery (SIMT SRS).

---

## Features

- Automatically detects all **Exp*** target structures
- Calculates:
  - Volume (cc)
  - Bounding-box maximum dimension (mm)
  - Equivalent-sphere diameter (mm)
  - Diameter ratio (equivalent-sphere diameter / bounding-box maximum dimension)
- Interactive WPF interface
- Sortable and resizable table
- Automatic handling of missing or invalid structures

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
- .NET Framework 4.5 or newer
- Microsoft Visual Studio

Required ESAPI assemblies (not included):

- `VMS.TPS.Common.Model.API.dll`
- `VMS.TPS.Common.Model.Types.dll`

These DLLs must be referenced from your local Eclipse installation.

---

## Installation

1. Clone the repository.

```bash
git clone https://github.com/Zhuoyun-Huang/srs-target-information.git
```

2. Open the project in Visual Studio.

3. Reference the ESAPI DLLs from your local Eclipse installation.

4. Build the script.

5. Launch the script from Eclipse.

---

## Workflow

1. Open a patient in Eclipse.
2. Load a valid Structure Set.
3. Run the script.
4. The software automatically detects all structures whose IDs begin with **"Exp"**.
5. A summary table is displayed containing:

- Structure ID
- Volume (cc)
- Bounding-box maximum dimension (mm)
- Equivalent-sphere diameter (mm)
- Diameter ratio

---

## Method

The script computes several geometric descriptors for each lesion.

### Equivalent-Sphere Diameter

The equivalent-sphere diameter is calculated from the lesion volume by assuming a sphere with the same volume.

### Bounding-Box Maximum Dimension

The maximum lesion dimension is estimated from the axis-aligned mesh bounding box provided by Eclipse.

### Diameter Ratio

The diameter ratio is defined as

```
min(Equivalent Sphere Diameter,
    Bounding-Box Maximum Dimension)
────────────────────────────────────
max(Equivalent Sphere Diameter,
    Bounding-Box Maximum Dimension)
```

This provides a simple geometric descriptor of lesion shape.

> **Note:** This diameter ratio is **not** the conventional three-dimensional sphericity metric.

---

## Example Output

For each Exp* lesion, the software reports:

| Structure | Volume (cc) | Bounding-box Max Dimension (mm) | Equivalent-Sphere Diameter (mm) | Diameter Ratio |
|-----------|------------:|--------------------------------:|--------------------------------:|---------------:|
| Exp01 | 1.82 | 18.6 | 15.1 | 0.81 |
| Exp02 | 0.94 | 14.3 | 12.2 | 0.85 |

---

## Disclaimer

This software is provided for **research and educational purposes only**.

It has **not** been validated for clinical use and should **not** be used as the sole basis for clinical decision-making.

Users are responsible for independently verifying all measurements before any clinical application.

---

## Author

**Zhuoyun Huang**

PhD Student, Medical Physics  
University at Buffalo

GitHub: https://github.com/Zhuoyun-Huang

LinkedIn: https://www.linkedin.com/in/zhuoyun-huang-0b55533bb/
