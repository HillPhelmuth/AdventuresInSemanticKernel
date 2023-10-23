// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using System.Diagnostics;

namespace SkPluginLibrary.Models.Helpers;

public static class PlanExtensions
{
    public static string ToPlanWithGoalString(this Plan plan, string indent = " ")
    {
        string goalHeader = $"{indent}Goal: {plan.Description}\n\n{indent}Steps:\n";

        return goalHeader + plan.ToPlanString();
    }
    public static async Task<Plan> ExecutePlanBySteps(this Plan plan, SKContext ctx, int maxSteps = 30)
    {
        Stopwatch sw = new();
        sw.Start();

        // loop until complete or at most N steps
        try
        {
            for (var step = 1; plan.HasNextStep && step < maxSteps; step++)
            {
                Console.WriteLine($"Starting Step {step}");
                plan = await plan.InvokeNextStepAsync(ctx);
                if (!plan.HasNextStep)
                {
                    Console.WriteLine($"Step {step} - COMPLETE!");
                    ctx.LoggerFactory.LogInformation("Step {step} - COMPLETE!", step);
                    Console.WriteLine(plan.State.ToString());
                    break;
                }

                Console.WriteLine($"Step {step} - Results so far:");
                Console.WriteLine(plan.State.ToString());
            }

            return plan;
        }
        catch (SKException e)
        {
            Console.WriteLine("Step - Execution failed:");
            Console.WriteLine(e.Message);
        }

        sw.Stop();
        Console.WriteLine($"Execution complete in {sw.ElapsedMilliseconds} ms!");
        return plan;
    }
}
