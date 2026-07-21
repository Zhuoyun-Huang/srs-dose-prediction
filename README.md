# SRS Dose Prediction

Machine learning framework for predicting normal-brain dose-volume metrics in single-isocenter multi-target (SIMT) stereotactic radiosurgery (SRS).

This project provides an Eclipse Scripting API (ESAPI) application that automatically extracts treatment planning features from Eclipse and predicts normal-brain dose-volume metrics using Gradient Boosted Regression Trees (GBRT). The software is intended to assist treatment planning by providing rapid estimates of V50%, V60%, and V66.7% before plan optimization.

---

## Features

- Automatic extraction of treatment planning features from Eclipse
- Prediction of:
  - **V50%**
  - **V60%**
  - **V66.7%**
- Multiple machine learning models:
  - Unclustered Individual
  - Unclustered Multi-output (3-in-1)
  - Clustered Individual
  - Clustered Multi-output (3-in-1)
- Automatic lesion clustering
- Prediction uncertainty estimation
- Interactive WPF graphical interface
- Automatic visualization of prediction results

---

## Repository Structure

```
SRS-Dose-Prediction/
│
├── GUI_SIMT_SRS_DosePrediction.cs
├── README.md
├── LICENSE
└── .gitignore
```

The trained model JSON files are required for prediction but are not included in this repository.

---

## Requirements

- Varian Eclipse
- Eclipse Scripting API (ESAPI)
- .NET Framework 4.5 or newer
- Microsoft Visual Studio

The following ESAPI assemblies are required and must be referenced from your local Eclipse installation:

- `VMS.TPS.Common.Model.API.dll`
- `VMS.TPS.Common.Model.Types.dll`

These proprietary DLLs are **not distributed** with this repository.

---

## Installation

1. Clone this repository.

```bash
git clone https://github.com/Zhuoyun-Huang/srs-dose-prediction.git
```

2. Open the project in Visual Studio.

3. Reference the ESAPI DLLs from your local Eclipse installation.

4. Place the trained model JSON files in the directory specified in the `Paths` class.

5. Build the project and launch the script through Eclipse.

---

## Workflow

1. Load a patient in Eclipse.
2. Select the target lesions.
3. The script automatically extracts geometric and dosimetric features.
4. Machine learning models estimate:
   - V50%
   - V60%
   - V66.7%
5. Prediction results and uncertainty estimates are displayed in the graphical interface.

---

## Method

The prediction models were developed using **Gradient Boosted Regression Trees (GBRT)**.

The framework supports:

- Individual-output models
- Multi-output (3-in-1) models
- Unclustered prediction
- Cluster-specific prediction

allowing comparison between different modeling strategies for predicting normal-brain dose-volume metrics in SIMT SRS.

---

## Publication

If you use this software in academic work, please cite:

> Huang Z, et al.
>
> *Machine learning prediction of normal-brain dose-volume metrics for single-isocenter multi-target stereotactic radiosurgery.*
>
> Journal of Radiosurgery and SBRT.

(Update with the final citation once published.)

---

## Disclaimer

This software is provided for **research and educational purposes only**.

It has **not** been approved for clinical use and should **not** be used as the sole basis for clinical decision-making.

Users are responsible for validating the software before any clinical application.

---

## Author

**Zhuoyun Huang**

PhD Student, Medical Physics  
University at Buffalo

GitHub: https://github.com/Zhuoyun-Huang

LinkedIn: https://www.linkedin.com/in/zhuoyun-huang-0b55533bb/
