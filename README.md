# Selah

**Selah** is a Windows desktop application for worship music preparation, project-based audio arrangement, and AI-assisted stem separation.

It is designed to help churches, missionaries, and small worship teams prepare accompaniment-oriented audio materials from recordings or worship media in a practical and accessible way.

> Current status: early-stage prototype / actively evolving architecture

---

## Overview

Selah combines three main ideas into one workflow:

- **project-based worship audio preparation**
- **timeline-oriented clip and track arrangement**
- **AI-assisted stem separation from audio recordings**

The application is currently implemented as a **WPF desktop app** with a shared core library for audio, project, and separation services.

---

## Main Goals

Selah aims to support:

- worship preparation for small churches and pioneer churches
- missionary and ministry-oriented audio workflows
- accompaniment preparation from worship recordings
- reusable project-based arrangement of separated stems
- simple local workflows without requiring cloud services

This project is being built as a **free and open-source tool** for non-commercial ministry-oriented use.

---

## Current Features

The repository currently includes:

- WPF desktop application (`Selah.App`)
- shared core library (`Selah.Core`)
- project / track / clip data model
- timeline-related UI and view models
- audio engine / mixer-related components
- waveform cache support
- FFmpeg / FFprobe wrapper service
- hardware detection service
- model management service
- prototype stem separation service using external Python + Demucs
- localization resources (Korean / English)
- theme resources (light / dark)

---

## Repository Structure

```text
Selah.sln
├─ src/
│  ├─ Selah.App/        # WPF application
│  └─ Selah.Core/       # audio engine, models, services
├─ scripts/
│  └─ demucs_runner.py  # external Demucs runner
├─ docs/
│  ├─ ETHICS.md
│  └─ TRADEMARK.md
├─ README.md
├─ LICENSE
└─ Selah.sln
