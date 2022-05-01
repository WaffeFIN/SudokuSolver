using System.Collections.Generic;

namespace SudokuSolver.Constraints;

[Constraint(DisplayName = "Sum Line", ConsoleName = "sumline")]
public class SumLineConstraint : Constraint
{
    public readonly List<(int, int)> endPoints;
    public readonly List<(int, int)> lineCells;
    private SumCellsHelper endPointsHelper;
    private SumCellsHelper lineSegment;
    private readonly HashSet<(int, int)> allCellsSet;

    public override string SpecificName => $"Sum Line from {CellName(endPoints[0])} - {CellName(endPoints[1])}";

    public SumLineConstraint(Solver solver, string options) : base(solver)
    {
        var cellGroups = ParseCells(options);
        if (cellGroups.Count != 2)
        {
            throw new ArgumentException($"Sum Line constraint expects 2 cell groups, got {cellGroups.Count}.");
        }

        endPoints = cellGroups[0];
        endPointsHelper = new(solver, endPoints);
        lineCells = cellGroups[1];
        lineSegment = new(solver, lineCells);
        allCellsSet = new(endPoints.Concat(lineCells));
    }

    public override LogicResult InitCandidates(Solver solver)
    {
        var (largestCount, smallestCount) = endPoints.Count > lineCells.Count
            ? (endPoints.Count, lineCells.Count)
            : (lineCells.Count, endPoints.Count);
        var minSum = largestCount;
        var maxSum = smallestCount * solver.MAX_VALUE;
        var possibleSums = Enumerable.Range(minSum, maxSum - minSum + 1);

        LogicResult result = endPointsHelper.Init(solver, possibleSums);
        LogicResult result2 = lineSegment.Init(solver, possibleSums);

        return result > result2 ? result : result2;
    }

    public override bool EnforceConstraint(Solver solver, int i, int j, int val)
    {
        if (!allCellsSet.Contains((i, j)))
        {
            return true;
        }

        return PossibleSums(solver).Count > 0;
    }

    public override LogicResult StepLogic(Solver solver, StringBuilder logicalStepDescription, bool isBruteForcing)
    {
        SortedSet<int> possibleSums = PossibleSums(solver);
        if (possibleSums.Count == 0)
        {
            logicalStepDescription?.Append("There are no possible sums that the sum line segments can be.");
            return LogicResult.Invalid;
        }

        bool changed = false;
        {
            var curLogicResult = endPointsHelper.StepLogic(solver, possibleSums, logicalStepDescription);
            if (curLogicResult == LogicResult.Invalid)
            {
                return LogicResult.Invalid;
            }
            changed |= curLogicResult == LogicResult.Changed;
        }
        {
            var curLogicResult = lineSegment.StepLogic(solver, possibleSums, logicalStepDescription);
            if (curLogicResult == LogicResult.Invalid)
            {
                return LogicResult.Invalid;
            }
            changed |= curLogicResult == LogicResult.Changed;
        }

        if (changed)
        {
            logicalStepDescription?.Append("Restricted Sum Line");
            return LogicResult.Changed;
        }

        return LogicResult.None;
    }

    private SortedSet<int> PossibleSums(Solver solver)
    {
        var segmentSums = lineSegment.PossibleSums(solver) ?? new();
        var endPointSums = endPointsHelper.PossibleSums(solver) ?? new();
        return new(segmentSums.Intersect(endPointSums));
    }
}
