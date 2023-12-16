using Godot;
using SharpNeat.Experiments;
using SharpNeat.NeuralNets;
﻿using SharpNeat.Neat.EvolutionAlgorithm;
using SharpNeat.Neat;
using SharpNeat.Neat.Genome.IO;
using SharpNeat.Neat.Reproduction.Asexual.WeightMutation;
using System.IO;
using System.Text;

#pragma warning disable

[GlobalClass]
public partial class Trainer : Node2D
{
    // Member variables here, example:
    [Export]
    public int PopulationSize = 10;
    [Export]
    public int Generations = 10;

    [Export]
    public bool LoadLatestBatch = true;

    [Export]
    public bool ShowDisplay = true;

    [Export]
    public bool SaveData = true;

    [Export]
    public bool LimitThreads = false;

    public static GamePool GamePool;

    private static Godot.Mutex mutex = new Godot.Mutex();

    public override void _Ready()
    {
        GD.Print($"Trainer Ready()");

        // Hide display?
        if (!ShowDisplay)
        {
            this.Visible = false;
        }

        // Instantiate shared game pool
        GamePool = new GamePool()
        {
            Trainer=this,
            Size=PopulationSize,
        };
        GamePool.Initialize();

        // Start training process
        try {
            train();
        } catch (Exception e)
        {
            GD.Print(e);
        }
    }

    private async void train()
    {
        List<NeatEvolutionAlgorithm<Double>> algorithms = new List<NeatEvolutionAlgorithm<Double>>(2);
        string[] teams = {"TeamA", "TeamB"};
        ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 2 };

        // Initialize algorithms concurrently for each team
        try 
        {
            await Parallel.ForEachAsync(
                teams,
                parallelOptions,
                async (team, ct) =>
                {
                    // Initialize algorithm
                    var ea = await InitializeAlgorithm(team);

                    // Add to list of initialized algorithms
                    mutex.Lock();
                    algorithms.Add(ea);
                    mutex.Unlock();
                });
        } catch (Exception e)
        {
            GD.Print(e);
        }
        
        // Save populations & results
        foreach (var ea in algorithms)
        {
            SavePopulation(ea);
            WriteGenerationResults(ea);
        }
        GD.Print("Initialized algorithms.\n");
                
        // Run each algorithm for each generation
        for (int i = 0; i < Generations; i++)
        {
            GD.Print($"Starting Gen[{i+1}]...");
            // Reset shared game pool
            GamePool.Initialize();

            // Run concurrently for each team
            try 
            {
                await Parallel.ForEachAsync(
                    algorithms,
                    parallelOptions,
                    async (ea, ct) =>
                    {
                        await RunAlgorithm(ea);
                    });
            } catch (Exception e)
            {
                GD.Print(e);
            }
            
            // Save populations & results
            foreach (var ea in algorithms)
            {
                SavePopulation(ea);
                WriteGenerationResults(ea);
            }
            GD.Print($"Finished Gen[{i+1}].\n");
        }

        // Finished!
        GD.Print("\nTraining finished!");
    }

    private async Task<NeatEvolutionAlgorithm<Double>> InitializeAlgorithm(String team)
    {        
        // TODO create better system for team-NPCType mapping
        String NPCType = "";
        if (team == "TeamA")
            NPCType = "Mushroom";
        else if (team == "TeamB")
            NPCType = "HumanSword";
        
        // Batch ID
        var BatchID = 0;
        var SaveFolder = $"./NEAT/Saves/{NPCType}";
        if (Directory.Exists(SaveFolder))
        {
            BatchID += Directory.GetDirectories(SaveFolder).Length;
        }

        // Experiment ID
        var Id = $"{NPCType}_Batch{BatchID}";
        GD.Print($"Initializing the EA for {Id}...");

        // Create the evaluation scheme
        var evalScheme = new EvaluationScheme()
        {
            GamePool=GamePool,
            Team=team,
        };
        // GD.Print("Initialized evaluation scheme.");

        // Determine max concurrent games to evaluate at once
        var degreeOfParallelism = -1;
        if (!LimitThreads) degreeOfParallelism = PopulationSize;
        
        // Create a NeatExperiment object with the evaluation scheme
        var experiment = new NeatExperiment<double>(evalScheme, Id)
        {
            IsAcyclic = true,
            ActivationFnName = ActivationFunctionId.LeakyReLU.ToString(),
            PopulationSize = PopulationSize,
            DegreeOfParallelism = degreeOfParallelism,
        };
        // GD.Print("Initialized experiment.");

        // Create a NeatEvolutionAlgorithm instance ready to run the experiment
        var ea = NeatUtils.CreateNeatEvolutionAlgorithm(experiment);

        // Load latest batch population?
        if (LoadLatestBatch)
        {
            var lastBatchPath = $"{SaveFolder}/Batch_{BatchID - 1}";
            if (Directory.Exists(lastBatchPath))
            {
                try {
                    // Calculate last generation path
                    var lastGen = Directory.GetDirectories(lastBatchPath).Length - 1;
                    var lastGenPath = $"{lastBatchPath}/Gen_{lastGen}";

                    // Create a MetaNeatGenome.
                    var metaNeatGenome = NeatUtils.CreateMetaNeatGenome(experiment);

                    // Load latest genome list
                    var populationLoader = new NeatPopulationLoader<Double>(metaNeatGenome);
                    var lastGenomeList = populationLoader.LoadFromFolder(lastGenPath);

                    // Create an instance of the default connection weight mutation scheme.
                    var weightMutationScheme = WeightMutationSchemeFactory.CreateDefaultScheme(experiment.ConnectionWeightScale);

                    // Create Population
                    var lastPopulation = NeatPopulationFactory<double>.CreatePopulation(
                        metaNeatGenome,
                        seedGenomes: lastGenomeList,
                        popSize: experiment.PopulationSize,
                        reproductionAsexualSettings: experiment.ReproductionAsexualSettings,
                        weightMutationScheme: weightMutationScheme 
                    );

                    // Recreate new algorithm
                    ea = NeatUtils.CreateNeatEvolutionAlgorithm(experiment, lastPopulation);

                    GD.Print($"Loaded existing population from {lastGenPath}");
                } catch (IOException e) {
                    GD.Print(e);
                }
            }
        }

        // Update algorithm meta data
        ea.BatchID = BatchID;
        ea.NPCType = NPCType;
        ea.StaticPopulationSize = PopulationSize;

        // Initialize the algorithm and run 0th generation
        await ea.Initialise();
        GD.Print($"Initialized the EA for {Id}!");

        return ea;
    }

    private async Task<NeatPopulation<Double>> RunAlgorithm(NeatEvolutionAlgorithm<Double> ea)
    {
        // Evaluate generation
        await ea.PerformOneGeneration();
        var neatPop = ea.Population;
        GD.Print($"({ea.NPCType}) Gen[{ea.Stats.Generation}] Fit_Best={neatPop.Stats.BestFitness.PrimaryFitness}, Fit_Mean={neatPop.Stats.MeanFitness}, Complexity_Mean={neatPop.Stats.MeanComplexity}, Complexity_Mode={ea.ComplexityRegulationMode}");         
        return neatPop;
    }

    private void WriteGenerationResults(NeatEvolutionAlgorithm<Double> ea)
    {
        if (!SaveData) return;
        const String fileName = "./NEAT/Saves/training_results.csv";
        const String separator = ",";
        StringBuilder output = new StringBuilder();
        var neatPop = ea.Population;
        try
        {
            // Build headings line if file doesn't exist
            if (!File.Exists(fileName))
            {
                String[] headings = {"Batch", "Gen", "NPC", "Fit_Best", "Fit_Mean", "Complexity_Mean"};
                output.AppendLine(string.Join(separator, headings));
            }

            // Build data line
            String[] data = {
                ea.BatchID.ToString(),
                ea.Stats.Generation.ToString(),
                ea.NPCType,
                neatPop.Stats.BestFitness.PrimaryFitness.ToString(),
                neatPop.Stats.MeanFitness.ToString(),
                neatPop.Stats.MeanComplexity.ToString()
            };
            output.AppendLine(string.Join(separator, data));

            // Append output to file
            File.AppendAllText(fileName, output.ToString());
            GD.Print($"Wrote generation results for {ea.NPCType}.");
        } catch (Exception e)
        {
            GD.Print(e);
        }
    }

    private void SavePopulation(NeatEvolutionAlgorithm<Double> ea)
    {
        if (!SaveData) return;
        try
        {
            GD.Print($"Attempting to save population for {ea.NPCType}");
            var folderName = $"{ea.NPCType}/Batch_{ea.BatchID}/Gen_{ea.Stats.Generation}";
            // ea.Population.BestGenome
            NeatPopulationSaver.SaveToFolder(
                ea.Population.GenomeList,
                "./NEAT/Saves/",
                folderName
            );
            GD.Print($"Saved population for {ea.NPCType}.");
        } catch (Exception e) {
            GD.Print(e);
        }
    }
    
}

