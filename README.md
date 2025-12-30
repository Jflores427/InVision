# InVision: Multimodal AR Assistant

> A hybrid Augmented Reality application combining on-device computer vision (YOLOv8) with cloud-based Visual Language Models (Gemini 2.5 Flash).

![Unity](https://img.shields.io/badge/Unity-6000.0-black?style=flat&logo=unity)
![ARCore](https://img.shields.io/badge/Platform-Android%20ARCore-green)
![License](https://img.shields.io/badge/License-MIT-blue)

## Overview

**InVision** is an Android AR application that allows users to identify and learn about physical objects in real-time. It utilizes a **Hybrid Compute Architecture**:
1.  **Edge Layer:** Runs a YOLOv8 Nano model locally via Unity Sentis for instant, offline object localization and 3D anchoring.
2.  **Cloud Layer:** Leverages the Google Gemini 1.5 Flash API for "Retrieval-Augmented Generation" (RAG), providing detailed, context-aware descriptions of anchored objects on demand.

The system solves common AR challenges like **Gaze-Prioritized Selection** (Center-Weighted logic) and **3D Spatial Mapping** from 2D bounding boxes.

---

## Key Features

* **Real-Time Detection:** Runs YOLOv8n inference at ~20-30 FPS on mobile GPUs.
* **Scan-on-Demand:** Thermal-efficient workflow; double-tap to scan, single-tap to interact.
* **Gaze Prioritization:** Automatically selects the object closest to the center of the screen, filtering out background clutter.
* **3D Anchoring:** Converts 2D detections into persistent 3D holographic wireframes using camera frustum geometry.
* **Multimodal Intelligence:** Captures high-res GPU snapshots to ask Gemini: *"What specific kind of [Object] is this?"*
* **Hybrid UI:** Features procedural scanning animations and "billboarded" 3D labels for readability.

---

## Tech Stack

* **Engine:** Unity 6 (Universal Render Pipeline)
* **AR Framework:** AR Foundation 6.0 (Google ARCore XR Plugin)
* **Inference Engine:** Unity Sentis 2.1.3
* **Model:** YOLOv8 Nano (`.onnx`, INT8 quantized)
* **Cloud API:** Google Gemini 2.5 Flash
* **Scripting:** C# (Asynchronous Compute, UnityWebRequest)

---

## Getting Started

### Prerequisites
* Unity Hub & Unity 6 (6000.0 or higher).
* Android Build Support module installed.
* An Android device compatible with ARCore.
* A valid [Google Gemini API Key](https://aistudio.google.com/).

### Installation
1.  **Clone the Repository**
    ```bash
    git clone [https://github.com/YourUsername/InVision-AR.git](https://github.com/YourUsername/InVision-AR.git)
    ```
2.  **Open in Unity**
    * Add the project to Unity Hub and open it.
    * Wait for the Package Manager to resolve dependencies (AR Foundation, Sentis, etc.).
3.  **Configure API Key**
    * Open the main scene: `Assets/Scenes/MainARScene.unity`.
    * Select the **XR Origin** object in the Hierarchy.
    * Find the `CloudQueryManager` component in the Inspector.
    * Paste your Gemini API Key into the **Api Key** field.
4.  **Configure API Key with .env File (Optional)**
    * Create a .env file in the root directory of the Unity Project.
    * Populate it with the following entry:
       ```bash
      GEMINI_API_KEY=<InsertGeminiAPIKeyHere>
      ```
    * Save the changes to the file.
5.  **Build to Android**
    * Go to **File > Build Settings**.
    * Switch Platform to **Android**.
    * Ensure your device is connected via USB (Debug Mode ON).
    * Click **Build and Run**.

---

## How to Use

1.  **Scanning (The "See" Phase)**
    * Point your camera at a table or desk.
    * **Double-Tap** anywhere on the screen.
    * A cyan scanning bar will appear. Move the phone slightly to detect objects (Cups, Laptops, Bottles, etc.).
    * After 4 seconds, the scan locks, and 3D wireframes are anchored to the objects.

2.  **Interacting (The "Think" Phase)**
    * **Single-Tap** on any Green Wireframe Cube.
    * A menu will appear displaying the detected class (e.g., "Cup").
    * Select **"Identify Specifics"**.

3.  **Result (The "Speak" Phase)**
    * The app captures a snapshot of the object and sends it to the Cloud.
    * Gemini will return a detailed description (e.g., *"This is a Starbucks ceramic mug..."*).

4.  **Cleanup**
    * Tap the **Clear** button in the top-right corner to wipe all anchors and start fresh.

---

## Architecture

The project follows a modular architecture managed by a central `ARAutomation` controller:

```mermaid
graph TD
    User[User Input] -->|Double Tap| Scan[ARAutomation: Scan Routine]
    Scan -->|GPU Texture| YOLO[YoloDetector: Sentis]
    YOLO -->|Bounding Boxes| Logic[Center-Weighted Logic]
    Logic -->|3D Raycast| Anchor[AR Anchor Manager]
    Anchor -->|Spawn| Wireframe[Wireframe Cube & Label]
    
    User -->|Single Tap| Interaction[InteractionUI]
    Interaction -->|Snapshot + Prompt| Cloud[CloudQueryManager]
    Cloud -->|JSON Request| Gemini[Google Gemini API]
    Gemini -->|Text Response| Interaction
