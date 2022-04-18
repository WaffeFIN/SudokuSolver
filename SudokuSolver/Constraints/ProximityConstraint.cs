namespace SudokuSolver.Constraints;

[Constraint(DisplayName = "Proximity", ConsoleName = "proximity")]
public class ProximityConstraint : Constraint
{
    private static readonly int PROXIMITY_DISTANCE = 2;// Proximity in the number of king's moves

    public readonly List<(int, int)> antiCells;
    public readonly HashSet<(int, int)> antiCellsSet;

    public ProximityConstraint(Solver sudokuSolver, string options) : base(sudokuSolver)
    {
        var cellGroups = ParseCells(options);
        if (cellGroups.Count > 1)
        {
            throw new ArgumentException($"Proximity constraint expects 0 or 1 cell groups, got {cellGroups.Count}.");
        }

        antiCells = cellGroups.Count == 0 ? new() : cellGroups[0];
        antiCellsSet = new(antiCells);
    }

    public override string SpecificName => $"Proximity";

    public override LogicResult InitCandidates(Solver solver)
    {
        var result = RemoveCandidates(solver);

        return !IsValid(solver) ? LogicResult.Invalid : result;
    }

    private LogicResult RemoveCandidates(Solver solver)
    {
        LogicResult result = LogicResult.None;
        foreach (var antiCell in antiCells)
        {
            LogicResult change = RemoveCandidates(solver, antiCell.Item1, antiCell.Item2);
            if (change == LogicResult.Invalid)
                return change;

            result = result == LogicResult.None ? change : result;
        }
        return result;
    }

    private LogicResult RemoveCandidates(Solver solver, int antiCellX, int antiCellY)
    {
        var antiCellMask = solver.Board[antiCellX, antiCellY];
        var result = LogicResult.None;
        if (!SolverUtility.IsValueSet(antiCellMask))
        {
            return result; // Some deductions could be made, but it's too fancy for me
        }

        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (dx == 0 && dy == 0 
                    || antiCellX + dx < 0 || antiCellX + dx >= solver.WIDTH
                    || antiCellY + dy < 0 || antiCellY + dy >= solver.HEIGHT)
                {
                    continue;
                }

                var mask = solver.Board[antiCellX + dx, antiCellY + dy];
                if (SolverUtility.IsValueSet(mask))
                {
                    continue;
                }
                var newMask = mask & ~antiCellMask;
                if (newMask != mask)
                {
                    result = LogicResult.Changed;
                }
                if (newMask == 0)
                {
                    return LogicResult.Invalid;
                }
                solver.Board[antiCellX + dx, antiCellY + dy] = newMask;
            }
        }
        return result;
    }

    private bool IsValid(Solver solver)
    {
        for (var x = 0; x < solver.WIDTH; x++)
        {
            for (var y = 0; y < solver.HEIGHT; y++)
            {
                var mask = solver.Board[x, y];
                if (SolverUtility.IsValueSet(mask) && !EnforceConstraint(solver, x, y, SolverUtility.GetValue(mask)))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public override bool EnforceConstraint(Solver solver, int i, int j, int val)
    {
        return antiCellsSet.Contains((i, j)) ?
            EnforceAntiCellConstraint(solver, i, j, SolverUtility.ValueMask(val)) :
            EnforceCellConstraint(solver, i, j, SolverUtility.ValueMask(val));
    }

    private bool EnforceAntiCellConstraint(Solver solver, int antiCellX, int antiCellY, uint antiCellMask)
    {
        // check that antiCell does not have a partner in proximity
        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (dx == 0 && dy == 0
                    || antiCellX + dx < 0 || antiCellX + dx >= solver.WIDTH
                    || antiCellY + dy < 0 || antiCellY + dy >= solver.HEIGHT)
                {
                    continue;
                }

                var mask = solver.Board[antiCellX + dx, antiCellY + dy];

                if ((mask & ~antiCellMask) == 0)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool EnforceCellConstraint(Solver solver, int cellX, int cellY, uint cellMask)
    {
        // check that cell has at least one possible partner in proximity
        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (dx == 0 && dy == 0
                    || cellX + dx < 0 || cellX + dx >= solver.WIDTH
                    || cellY + dy < 0 || cellY + dy >= solver.HEIGHT)
                {
                    continue;
                }

                var mask = solver.Board[cellX + dx, cellY + dy];

                if ((mask & cellMask) != 0)
                {
                    // Found one
                    return true;
                }
            }
        }
        return false;
    }

    public override LogicResult StepLogic(Solver solver, StringBuilder logicalStepDescription, bool isBruteForcing)
    {
        var result = RemoveCandidates(solver); // TODO: No need to loop through them all each step
        if (result != LogicResult.None)
        {
            logicalStepDescription?.Append($"Removed candidates seen by anti-proximity cells");
            return result;
        }

        return isBruteForcing ? LogicResult.None : CheckUnorthodoxHiddenSingles(solver, logicalStepDescription);
    }

    private LogicResult CheckUnorthodoxHiddenSingles(Solver solver, StringBuilder logicalStepDescription)
    {
        // check for Unorthodox hidden singles
        var result = LogicResult.None;
        for (var x = 0; x < solver.WIDTH; x++)
        {
            for (var y = 0; y < solver.HEIGHT; y++)
            {
                if (antiCells.Contains((x, y))) {
                    continue;
                }
                var mask = solver.Board[x, y];
                if (SolverUtility.IsValueSet(mask))
                {
                    var hiddenSinglesResult = CheckUnorthodoxHiddenSingles(solver, x, y, mask & ~SolverUtility.valueSetMask);
                    if (hiddenSinglesResult.result == LogicResult.Invalid)
                    {
                        logicalStepDescription?.Append($"Cell at {SolverUtility.CellName((x, y))} has no possible partner in proximity");
                        return LogicResult.Invalid;
                    }
                    if (hiddenSinglesResult.result == LogicResult.Changed)
                    {
                        var value = SolverUtility.GetValue(mask);
                        logicalStepDescription?.Append($"Unorthodox hidden single {value} at {SolverUtility.CellName(hiddenSinglesResult.cell.Value)}");
                        solver.Board[hiddenSinglesResult.cell.Value.Item1, hiddenSinglesResult.cell.Value.Item2] = mask & ~SolverUtility.valueSetMask;
                        return LogicResult.Changed;
                    }
                }
            }
        }
        return result;
    }

    struct UnorthodoxSingleResult
    {
        internal (int, int)? cell;
        internal LogicResult result;
    }

    private UnorthodoxSingleResult CheckUnorthodoxHiddenSingles(Solver solver, int cellX, int cellY, uint cellValueMask)
    {
        (int, int)? found = null;
        // check that cell has at least one possible partner in proximity
        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (dx == 0 && dy == 0
                    || cellX + dx < 0 || cellX + dx >= solver.WIDTH
                    || cellY + dy < 0 || cellY + dy >= solver.HEIGHT)
                {
                    continue;
                }

                var mask = solver.Board[cellX + dx, cellY + dy];

                if ((mask & cellValueMask) != 0)
                {
                    if (SolverUtility.IsValueSet(mask))
                    {
                        // Cell is already connected, no unorthodox hidden singles can be deduced
                        return new UnorthodoxSingleResult
                        {
                            cell = null,
                            result = LogicResult.None
                        };
                    }
                    if (found == null)
                    {
                        // Found one
                        found = ((cellX + dx, cellY + dy));
                    }
                    else
                    {
                        // Found two+
                        return new UnorthodoxSingleResult {
                            cell = null,
                            result = LogicResult.None
                        };
                    }
                }
            }
        }

        return found == null
            ? new UnorthodoxSingleResult
            {
                cell = null,
                result = LogicResult.Invalid
            }
            : new UnorthodoxSingleResult
            {
                cell = found,
                result = LogicResult.Changed
            };
    }
}
