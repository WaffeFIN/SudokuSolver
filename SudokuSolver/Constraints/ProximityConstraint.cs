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

        var result2 = RemoveCandidatesFromAntiCells(solver);

        return result2 == LogicResult.None ? result : result2;
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
                if (newMask == 0)
                {
                    return LogicResult.Invalid;
                }
                if (newMask != mask)
                {
                    result = LogicResult.Changed;
                    solver.Board[antiCellX + dx, antiCellY + dy] = newMask;
                }
            }
        }
        return result;
    }

    private LogicResult RemoveCandidatesFromAntiCells(Solver solver)
    {
        var result = LogicResult.None;
        for (var x = 0; x < solver.WIDTH; x++)
        {
            for (var y = 0; y < solver.HEIGHT; y++)
            {
                var mask = solver.Board[x, y];
                if (SolverUtility.IsValueSet(mask) && !antiCellsSet.Contains((x, y)))
                {
                    var newResult = RemoveCandidatesFromAntiCells(solver, x, y, mask);
                    result = result > newResult ? result : newResult;
                }
            }
        }
        return result;
    }

    private LogicResult RemoveCandidatesFromAntiCells(Solver solver, int cellX, int cellY, uint cellMask)
    {
        var result = LogicResult.None;
        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (dx == 0 && dy == 0
                    || cellX + dx < 0 || cellX + dx >= solver.WIDTH
                    || cellY + dy < 0 || cellY + dy >= solver.HEIGHT
                    || !antiCellsSet.Contains((cellX + dx, cellY + dy)))
                {
                    continue;
                }

                var antiCellMask = solver.Board[cellX + dx, cellY + dy] & ~SolverUtility.valueSetMask;

                var newMask = ~cellMask & antiCellMask;
                if (newMask == 0)
                {
                    return LogicResult.Invalid;
                }
                if (newMask != antiCellMask)
                {
                    result = LogicResult.Changed;
                    solver.Board[cellX + dx, cellY + dy] = newMask;
                }
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
            EnforceAntiCellConstraint(solver, i, j, val) :
            EnforceCellConstraint(solver, i, j, val);
    }

    private bool EnforceAntiCellConstraint(Solver solver, int antiCellX, int antiCellY, int antiCellValue)
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

                if (SolverUtility.IsValueSet(mask) && SolverUtility.GetValue(mask) == antiCellValue)
                {
                    return false;
                }
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint MaskFromVal(int val)
    {
        return 1u << (val - 1);
    }

    private bool EnforceCellConstraint(Solver solver, int cellX, int cellY, int cellValue)
    {
        // check that no anti cells are in proximity
        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (antiCellsSet.Contains((cellX + dx, cellY + dy)))
                {
                    var antiCellMask = solver.Board[cellX + dx, cellY + dy];
                    if (SolverUtility.IsValueSet(antiCellMask) && SolverUtility.GetValue(antiCellMask) == cellValue)
                    {
                        // Sees anti cell
                        return false;
                    }
                }
            }
        }

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

                if ((mask & MaskFromVal(cellValue)) != 0)
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
        if (!IsValid(solver))
        {
            logicalStepDescription?.Append($"Proximity constraint broken");
            return LogicResult.Invalid;
        }
        var result = RemoveCandidates(solver); // TODO: No need to loop through them all each step
        if (result != LogicResult.None)
        {
            logicalStepDescription?.Append($"Removed candidates seen by anti-proximity cells");
            return result;
        }

        return CheckUnorthodoxHiddenSingles(solver, logicalStepDescription);
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
                    var hiddenSinglesResult = CheckUnorthodoxHiddenSingle(solver, x, y, mask & ~SolverUtility.valueSetMask);
                    if (hiddenSinglesResult.result == LogicResult.Invalid)
                    {
                        logicalStepDescription?.Append($"Cell at {SolverUtility.CellName((x, y))} has no possible partner in proximity");
                        return LogicResult.Invalid;
                    }
                    if (hiddenSinglesResult.result == LogicResult.Changed)
                    {
                        var value = SolverUtility.GetValue(mask);

                        logicalStepDescription?.Append(
                            result != LogicResult.Changed
                            ? $"Unorthodox hidden single(s) {value} at {SolverUtility.CellName(hiddenSinglesResult.cell.Value)}"
                            : $", {value} at {SolverUtility.CellName(hiddenSinglesResult.cell.Value)}"
                        );


                        if (!solver.SetValue(
                            hiddenSinglesResult.cell.Value.Item1,
                            hiddenSinglesResult.cell.Value.Item2,
                            value))
                        {
                            logicalStepDescription?.Append($"Unable to place unorthodox hidden single {value} at {SolverUtility.CellName(hiddenSinglesResult.cell.Value)}");
                            return LogicResult.Invalid;
                        }
                        result = LogicResult.Changed;
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

    private UnorthodoxSingleResult CheckUnorthodoxHiddenSingle(Solver solver, int cellX, int cellY, uint cellValueMask)
    {
        (int, int)? found = null;
        // check that cell has at least one possible partner in proximity
        for (int dx = -PROXIMITY_DISTANCE; dx <= PROXIMITY_DISTANCE; dx++)
        {
            for (int dy = -PROXIMITY_DISTANCE; dy <= PROXIMITY_DISTANCE; dy++)
            {
                if (dx == 0 && dy == 0
                    || cellX + dx < 0 || cellX + dx >= solver.WIDTH
                    || cellY + dy < 0 || cellY + dy >= solver.HEIGHT
                    || antiCellsSet.Contains((cellX + dx, cellY + dy))) // We can't place a hidden single in an anti cell
                {
                    continue;
                }

                var mask = solver.Board[cellX + dx, cellY + dy];

                if ((mask & cellValueMask) != 0)
                {
                    if (SolverUtility.IsValueSet(mask) || found != null)
                    {
                        // Either: Cell is already connected, no unorthodox hidden singles can be deduced
                        // Or: we found two+ candidates
                        return new UnorthodoxSingleResult
                        {
                            cell = null,
                            result = LogicResult.None
                        };
                    }
                    found = ((cellX + dx, cellY + dy));
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
