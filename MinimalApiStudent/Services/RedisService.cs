
using Microsoft.Extensions.Caching.Distributed;
//using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using MinimalApiStudent.Models;

namespace MinimalApiStudent.Services
{
    public class RedisService
    {
        private readonly IDistributedCache _cache;

        public RedisService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<List<Class>> GetClassesFromRedisAsync()
        {
            var cacheKey = "classes"; // Cache key where classes' data is stored

            var cachedclasses = await _cache.GetStringAsync(cacheKey);
            if (cachedclasses != null)
            {
                // If data is found in the cache, deserialize and return it
                var classes = JsonSerializer.Deserialize<List<Class>>(cachedclasses);
                return classes;
            }

            return null; // Data not found in the cache
        }

        public async Task<List<Student>> GetStudentsFromRedisAsync()
        {
            var cacheKey = "students"; // Cache key where students' data is stored

            var cachedStudents = await _cache.GetStringAsync(cacheKey);
            if (cachedStudents != null)
            {
                // If data is found in the cache, deserialize and return it
                var students = JsonSerializer.Deserialize<List<Student>>(cachedStudents);
                return students;
            }

            return null; // Data not found in the cache
        }

        public async Task<List<Attendance>> GetAttendancesFromRedisAsync()
        {
            var cacheKey = "attendances"; // Cache key where classes' data is stored

            var cachedattendances = await _cache.GetStringAsync(cacheKey);
            if (cachedattendances != null)
            {
                // If data is found in the cache, deserialize and return it
                var attendances = JsonSerializer.Deserialize<List<Attendance>>(cachedattendances);
                return attendances;
            }

            return null; // Data not found in the cache
        }




    }

}

