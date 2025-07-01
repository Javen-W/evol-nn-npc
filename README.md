# NeuroEvolutionary Game AI for Adversarial Environment

This repository contains the term project for CSE848 Evolutionary Computation (Fall 2023), implementing a 2D platformer battle environment in Godot 4 with C# and GDScript. The project uses the NEAT (NeuroEvolution of Augmenting Topologies) algorithm, via a modified SharpNEAT library, to evolve neural networks controlling adversarial NPC behaviors (Mushroom vs. HumanSword teams). The system trains NPCs to optimize combat strategies in a simulated arena, evaluating fitness based on match outcomes, damage, and survival time, with applications to dynamic system modeling in manufacturing.

## Table of Contents
- [Evolutionary Neural Network for Game NPC AI](#evolutionary-neural-network-for-game-npc-ai)
  - [Project Overview](#project-overview)
  - [Approach](#approach)
  - [Tools and Technologies](#tools-and-technologies)
  - [Results](#results)
  - [Skills Demonstrated](#skills-demonstrated)
  - [Setup and Usage](#setup-and-usage)
  - [References](#references)

## Project Overview
The project develops a 2D platformer battle environment in Godot 4, where two NPC teams (TeamA: Mushroom, TeamB: HumanSword) compete in adversarial matches. Each NPC is controlled by a neural network evolved using NEAT, implemented via a modified SharpNEAT library. The system trains NPCs over 10 generations with a population of 10 genomes per team, optimizing behaviors (movement, jumping, attacking) based on a fitness function that rewards winning, minimizing damage received, and maximizing damage dealt. Matches run for up to 60 seconds, with asynchronous training and multithreaded evaluation for efficiency. Results are logged and visualized using pandas and Matplotlib, applicable to optimizing dynamic systems in manufacturing.

## Approach
The project is structured as a modular system:
- **Game Environment (e.g., `Game.gd`, `Map.gd`, `CharacterComponent.gd`, `AttackComponent.gd`)**: Implements a 2D platformer arena with physics-based movement (walking, jumping, gravity) and combat mechanics. NPCs (Mushroom, HumanSword) are spawned on a tilemap, with states (Idle, Walk, Jump, Attack, Dead) and attributes (health, attack cooldown, knockback).
- **NEAT Training (`Trainer.cs`, `Evaluator.cs`)**: Uses a modified SharpNEAT library to evolve neural networks for each team. Each genome controls an NPC’s actions via 11 inputs (e.g., health, enemy distance, attack cooldowns) and 3 outputs (x-movement, jump, attack). Fitness is calculated based on match outcomes (win: +5, tie: -5), time survived, damage received, and damage dealt.
- **Game Pool (`Gamepool.cs`)**: Manages a pool of game sessions (size=10), instantiating matches with paired NPCs from opposing teams. Uses mutex locks for thread-safe game assignment and asynchronous evaluation via Godot’s signal system.
- **NPC Control (`NNBrainComponent.cs`, `Inputstate.cs`)**: Maps neural network outputs to NPC decisions (move left/right, jump, attack) using input states (e.g., health, enemy position). Fallback `MobBrainComponent.gd` provides random movement for baseline testing.
- **Navigation (`NavigationComponent.gd`)**: Handles pathfinding to enemy positions or random tiles, with cooldowns to prevent excessive updates.
- **Result Analysis (`analyze_results.py`)**: Processes training logs (`training_results.csv`) with pandas, generating plots of mean/best fitness and network complexity per generation using Matplotlib.

The system evaluates NPC performance in parallel, saving population genomes and logging fitness metrics (best/mean fitness, complexity) per generation for analysis.

## Tools and Technologies
- **Godot 4**: Game engine for 2D platformer simulation and NPC interactions.
- **C#**: Primary language for NEAT training, game pool management, and NPC control.
- **GDScript**: Used for game logic, character movement, and navigation components.
- **SharpNEAT (Modified)**: Neuroevolution framework for evolving NPC neural networks.
- **pandas**: Parsing training results for analysis.
- **Matplotlib**: Visualizing fitness and complexity trends.
- **Godot Signals**: Asynchronous communication for game events (e.g., match completion, character death).
- **Multithreading**: Parallel evaluation of game sessions using C#’s `Parallel.ForEachAsync`.

## Results
- **Training**: Evolved neural networks for two NPC teams over many generations (_n_ genomes each), optimizing combat strategies in a 60-second adversarial match environment.
- **Performance**: Achieved adaptive NPC behaviors, with fitness scores based on winning matches, minimizing damage received, and maximizing damage dealt (specific metrics logged in `training_results.csv`).
- **Visualization**: Generated plots of mean/best fitness and network complexity, enabling analysis of evolutionary progress.
- **Scalability**: Supported parallel evaluation of multiple game sessions, with thread-safe game pool management and asynchronous training.

## Skills Demonstrated
- **Neuroevolution**: Implemented NEAT to evolve NPC behaviors, applicable to optimizing manufacturing control systems.
- **Game Development**: Designed a 2D platformer with physics-based movement and combat in Godot 4.
- **Asynchronous Programming**: Built an asynchronous training system using Godot signals and C# tasks.
- **Multithreading**: Utilized parallel processing for efficient genome evaluation.
- **Data Analysis**: Processed training logs with pandas and visualized results with Matplotlib.
- **Software Engineering**: Developed a modular system with robust error handling and logging.

## Setup and Usage
1. **Prerequisites**:
   - Clone the repository: `git clone https://github.com/Javen-W/evol-nn-npc`
   - Install Godot 4: [Godot Download](https://godotengine.org/download)
   - Install Python dependencies for analysis: `pip install pandas matplotlib`
2. **Running**:
- Open the project in Godot 4 and press “Run” to start training (10 generations, 10 genomes per team).
- Adjust `Trainer.cs` parameters (e.g., `PopulationSize`, `Generations`) for different configurations.
- Run `analyze_results.py` to generate fitness and complexity plots from `training_results.csv`.
3. **Notes**:
- Requires Godot 4 with C# support enabled.
- Set `ShowDisplay=false` in `Trainer.cs` to hide the game visualization for faster training.
- Ensure write permissions for `./NEAT/Saves/` to store genomes and results.

## References
- [NEAT Paper](https://nn.cs.utexas.edu/?stanley:ec02)
- [SharpNEAT Documentation](https://sharpneat.sourceforge.io/)
- [Godot 4 Documentation](https://docs.godotengine.org/en/stable/)
- [pandas Documentation](https://pandas.pydata.org/docs/)
- [Matplotlib Documentation](https://matplotlib.org/stable/contents.html)
- [Project Repository](https://github.com/Javen-W/evol-nn-npc)
