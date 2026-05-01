using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class EvaluationService
    {
        private readonly AppDbContext _context;

        public EvaluationService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Creates a new evaluation for a project
        /// </summary>
        public async Task<Evaluation?> CreateEvaluation(int projectId, int evaluatorId)
        {
            try
            {
                // Verify project and evaluator exist
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                var evaluator = await _context.Users.FirstOrDefaultAsync(u => u.Id == evaluatorId);

                if (project == null || evaluator == null)
                    return null;

                var evaluation = new Evaluation
                {
                    ProjectId = projectId,
                    EvaluatorId = evaluatorId,
                    TotalScore = 0,
                    Feedback = string.Empty,
                    IsLocked = false,
                    EvaluatedAt = DateTime.UtcNow
                };

                _context.Evaluations.Add(evaluation);
                await _context.SaveChangesAsync();
                return evaluation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create evaluation error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Adds or updates a rubric score for an evaluation
        /// </summary>
        public async Task<bool> AddRubricScore(int evaluationId, int rubricItemId, decimal obtainedMarks)
        {
            try
            {
                var evaluation = await _context.Evaluations
                    .FirstOrDefaultAsync(e => e.Id == evaluationId);

                if (evaluation == null || evaluation.IsLocked)
                    return false;

                var rubricItem = await _context.RubricItems
                    .FirstOrDefaultAsync(ri => ri.Id == rubricItemId);

                if (rubricItem == null || obtainedMarks > rubricItem.MaxMarks || obtainedMarks < 0)
                    return false;

                // Check if score already exists
                var existingScore = await _context.RubricScores
                    .FirstOrDefaultAsync(rs => rs.EvaluationId == evaluationId && rs.RubricItemId == rubricItemId);

                if (existingScore != null)
                {
                    existingScore.ObtainedMarks = obtainedMarks;
                    _context.RubricScores.Update(existingScore);
                }
                else
                {
                    var rubricScore = new RubricScore
                    {
                        EvaluationId = evaluationId,
                        RubricItemId = rubricItemId,
                        ObtainedMarks = obtainedMarks
                    };

                    _context.RubricScores.Add(rubricScore);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Add rubric score error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculates the total score for an evaluation by summing all rubric scores
        /// </summary>
        public async Task<decimal> CalculateTotalScore(int evaluationId)
        {
            try
            {
                var evaluation = await _context.Evaluations
                    .Include(e => e.RubricScores)
                    .FirstOrDefaultAsync(e => e.Id == evaluationId);

                if (evaluation == null)
                    return 0;

                var totalScore = evaluation.RubricScores.Sum(rs => rs.ObtainedMarks);

                evaluation.TotalScore = totalScore;
                _context.Evaluations.Update(evaluation);
                await _context.SaveChangesAsync();

                return totalScore;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Calculate total score error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Locks an evaluation to prevent further modifications
        /// </summary>
        public async Task<bool> LockEvaluation(int evaluationId)
        {
            try
            {
                var evaluation = await _context.Evaluations.FirstOrDefaultAsync(e => e.Id == evaluationId);
                if (evaluation == null)
                    return false;

                evaluation.IsLocked = true;
                _context.Evaluations.Update(evaluation);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lock evaluation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves evaluation for a specific project
        /// </summary>
        public async Task<Evaluation?> GetEvaluationByProject(int projectId)
        {
            try
            {
                return await _context.Evaluations
                    .Include(e => e.RubricScores)
                    .FirstOrDefaultAsync(e => e.ProjectId == projectId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluation by project error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves all evaluations for a user (evaluator)
        /// </summary>
        public async Task<List<Evaluation>> GetUserEvaluations(int userId)
        {
            try
            {
                return await _context.Evaluations
                    .Where(e => e.EvaluatorId == userId)
                    .Include(e => e.RubricScores)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get user evaluations error: {ex.Message}");
                return new List<Evaluation>();
            }
        }

        /// <summary>
        /// Retrieves evaluations by project ID
        /// </summary>
        public async Task<List<Evaluation>> GetEvaluationsByProject(int projectId)
        {
            try
            {
                return await _context.Evaluations
                    .Where(e => e.ProjectId == projectId)
                    .Include(e => e.RubricScores)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluations by project error: {ex.Message}");
                return new List<Evaluation>();
            }
        }

        /// <summary>
        /// Gets evaluation by ID
        /// </summary>
        public async Task<Evaluation?> GetEvaluationById(int id)
        {
            try
            {
                return await _context.Evaluations
                    .Include(e => e.RubricScores)
                    .FirstOrDefaultAsync(e => e.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluation by id error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates evaluation feedback
        /// </summary>
        public async Task<bool> UpdateFeedback(int evaluationId, string feedback)
        {
            try
            {
                var evaluation = await _context.Evaluations.FirstOrDefaultAsync(e => e.Id == evaluationId);
                if (evaluation == null || evaluation.IsLocked)
                    return false;

                evaluation.Feedback = feedback;
                _context.Evaluations.Update(evaluation);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update feedback error: {ex.Message}");
                return false;
            }
        }
    }
}
