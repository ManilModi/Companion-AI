using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace DotnetMVCApp.Repositories
{
    public class JobRepo : IJobRepo
    {
        private readonly AppDbContext _context;

        public JobRepo(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Job> GetAllJobs()
        {
            return _context.Jobs
                .Include(j => j.PostedBy)
                .Include(j => j.Applicants)
                .Include(j => j.Interviews)
                .Include(j => j.Feedbacks)
                .ToList();
        }

        public Job GetJobById(int id)
        {
            return _context.Jobs
                .Include(j => j.PostedBy)
                .Include(j => j.Applicants)
                .ThenInclude(a => a.User)
                .ThenInclude(u => u.Feedbacks)   // ✅ make sure User.Feedbacks are loaded
                .Include(j => j.Interviews)
                .Include(j => j.Feedbacks)
                .FirstOrDefault(j => j.JobId == id);
        }


        public Job Add(Job job)
        {
            _context.Jobs.Add(job);
            _context.SaveChanges();
            return job;
        }

        public Job Update(Job job)
        {
            _context.Jobs.Update(job);
            _context.SaveChanges();
            return job;
        }

        public Job Delete(int id)
        {
            var job = _context.Jobs.Find(id);
            if (job != null)
            {
                _context.Jobs.Remove(job);
                _context.SaveChanges();
            }
            return job;
        }

        public Job GetJobWithApplicantsAndUsers(int jobId)
        {
            return _context.Jobs
                .Include(j => j.Applicants)
                    .ThenInclude(uj => uj.User)
                .FirstOrDefault(j => j.JobId == jobId);
        }


        public async Task<Job> AddJobAsync(Job job)
        {
            if (string.IsNullOrWhiteSpace(job.JobDescription))
                throw new Exception("Job description cannot be empty.");

            // Call FastAPI /embed endpoint to get job description embedding
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsJsonAsync("http://127.0.0.1:8000/embed", new { text = job.JobDescription });

            if (!response.IsSuccessStatusCode)
                throw new Exception($"FastAPI embedding service failed: {response.StatusCode}");

            var json = await response.Content.ReadFromJsonAsync<EmbedResponse>();
            if (json?.Embedding == null || json.Embedding.Length == 0)
                throw new Exception("Embedding service returned empty vector.");

            // Store vector in DB
            job.Embedding = json.Embedding;

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            return job;
        }


        public class EmbedResponse
    {
        public float[] Embedding { get; set; }
    }

        public async Task<List<Job>> GetTopJobsByCandidateEmbeddingAsync(float[] candidateEmbedding, int topN = 10)
        {
            if (candidateEmbedding == null || candidateEmbedding.Length == 0)
                return new List<Job>();

            var embeddingJson = JsonSerializer.Serialize(candidateEmbedding);

            // Raw SQL with cosine similarity function (make sure you have it defined in PostgreSQL)
            return await _context.Jobs
                .FromSqlRaw(@"
            SELECT *, cosine_similarity(""Embedding"", {0}::jsonb) AS similarity
            FROM ""Jobs""
            ORDER BY similarity DESC
            LIMIT {1}", embeddingJson, topN)
                .ToListAsync();
        }

    }
}
