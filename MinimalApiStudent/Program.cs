//using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using MinimalApiStudent;
using MinimalApiStudent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.JsonPatch;
using StackExchange.Redis;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using MinimalApiStudent.Services;

using MinimalApiStudent.DTO;
using Polly;
using System.Configuration;
using Microsoft.Extensions.Options;
//using MinimalApiStudent.Configuration;
using MinimalApiStudent.Services;
using Microsoft.Extensions.Caching.Distributed;
using Sieve.Services;
//using System.Text.Json;
//using SieveSettings = Sieve.Models.SieveSettings;

var builder = WebApplication.CreateBuilder(args);

// Replace this with your Redis server details
//var redisConfiguration = "localhost:6379"; // Redis server address


// Configure services for caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379"; // Redis server running on localhost with the default port (6379)
    options.InstanceName = "MyCache"; // You can provide a unique instance name

});

// Register the RedisCacheService
builder.Services.AddScoped<RedisService>();
builder.Services.AddSingleton<SieveProcessor>();

//-----------------------------------------------------------------------------------------


//JsonPatch ----------------------------------
builder.Services.AddScoped<JsonPatchService>();
builder.Services.AddScoped<JsonPatchDocument>();




builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
});
//-----------------------------------------
//connection string for sql server 

builder.Services.AddDbContext<StudentAPIDBcontext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnectionNew"));
});

//-------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var app = builder.Build();

//----------------------------------------------------------


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseHttpsRedirection();

//-------------------Redis Endpoints---------------------------------


//--------------------------------Endpoints for students-------------------------------------------
//Get all the students without redis 
//app.MapGet("/api/students", (StudentAPIDBcontext dbContext) =>
//{

//    var students = dbContext.Students.ToList();
//    return Results.Ok(students);
//});


app.MapGet("/api/students", async (StudentAPIDBcontext dbContext, IDistributedCache cache ) =>
{
    var cacheKey = "students"; // Cache key for storing students data

    // Try to get students from the cache
    var cachedStudents = await cache.GetStringAsync(cacheKey);
    if (cachedStudents != null)
    {
        // If data is found in the cache, return it
        var students = JsonSerializer.Deserialize<List<Student>>(cachedStudents);

        return Results.Ok(students);
    }

    // If data is not in the cache, fetch from the database
    var studentsFromDb = dbContext.Students.ToList();

    // Serialize and store students data in the cache for a specified duration
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(studentsFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Ok(studentsFromDb);
    
});

//----------------------------------------------Redis Student GET endpoints ------------------

app.MapGet("/api/students/redis", async (RedisService redisCacheService) =>
{
    var studentsFromRedis = await redisCacheService.GetStudentsFromRedisAsync();
    if (studentsFromRedis != null)
    {
        return Results.Ok(studentsFromRedis);
    }

    return Results.NotFound("Students data not found in Redis.");
});

app.MapGet("/api/classes/redis", async (RedisService redisCacheService) =>
{
    var classesFromRedis = await redisCacheService.GetClassesFromRedisAsync();
    if (classesFromRedis != null)
    {
        return Results.Ok(classesFromRedis);
    }

    return Results.NotFound("classes data not found in Redis.");
});

app.MapGet("/api/attendance/redis", async (RedisService redisCacheService) =>
{
    var attendancesFromRedis = await redisCacheService.GetAttendancesFromRedisAsync();
    if (attendancesFromRedis != null)
    {
        return Results.Ok(attendancesFromRedis);
    }

    return Results.NotFound("attendances data not found in Redis.");
});


//--------------------------------------------------------------------------------
//----------------------------------------------------------------------------------------------------
//Get students by id  without redis 
//app.MapGet("/api/students/{id:int}", (int id, StudentAPIDBcontext dbContext) =>
//{
//    var student = dbContext.Students.Find(id);
//    if (student == null)
//    {
//        return Results.NotFound();
//    }
//    return Results.Ok(student);
//});

//----------------------------------------------------------------------------
//Get students by id  with redis 

app.MapGet("/api/students/{id:int}", async (int id, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var cacheKey = "student_" + id; // Cache key for storing a specific student's data

    // Try to get the student from the cache
    var cachedStudent = await cache.GetStringAsync(cacheKey);
    if (cachedStudent != null)
    {
        // If data is found in the cache, return it
        var student = JsonSerializer.Deserialize<Student>(cachedStudent);
        return Results.Ok(student);
    }

    // If data is not in the cache, fetch from the database
    var studentFromDb = dbContext.Students.Find(id);

    if (studentFromDb == null)
    {
        return Results.NotFound();
    }

    // Serialize and store the student data in the cache for a specified duration
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(studentFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Ok(studentFromDb);
});




//---------------------------------------------------------------------------------------------------

//post student 
//without redis cache 

//app.MapPost("/api/students", async (Student student, StudentAPIDBcontext dbContext) =>
//{
//    dbContext.Students.Add(student);
//    await dbContext.SaveChangesAsync();
//    return Results.Created($"/api/students/{student.StudentId}", student);
//});

//with redis cache
app.MapPost("/api/students", async (Student student, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    // Add the student to the database
    dbContext.Students.Add(student);
    await dbContext.SaveChangesAsync();

    // Store the student in Redis cache
    var cacheKey = "students"; // Cache key for storing students data

    // Fetch the existing cached students or create an empty list
    var existingCachedStudents = await cache.GetStringAsync(cacheKey);
    List<Student> studentsToCache = existingCachedStudents != null
        ? JsonSerializer.Deserialize<List<Student>>(existingCachedStudents)
        : new List<Student>();

    // Add the new student to the list
    studentsToCache.Add(student);

    // Serialize and store the updated list in the cache
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(studentsToCache), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Created($"/api/students/{student.StudentId}", student);
});




//--------------------------------------------------------------------------------------------------------
//update student
/*app.MapPut("/api/students/{id:int}", async (int id, Student updatedStudent, StudentAPIDBcontext dbContext) =>
{
    var student = dbContext.Students.Find(id);
    if (student == null)
    {
        return Results.NotFound();
    }

    student.StudentName = updatedStudent.StudentName;
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});*/

app.MapPut("/api/students/{id:int}", async (int id, Student updatedStudent, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var student = dbContext.Students.Find(id);
    if (student == null)
    {
        return Results.NotFound();
    }

    // Update the student data in the database
    student.StudentName = updatedStudent.StudentName;
    await dbContext.SaveChangesAsync();

    // Update the cache with the updated data
    var cacheKey = "students";

    // Replace the cached data with the updated list of students
    var studentsFromDb = dbContext.Students.ToList();
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(studentsFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.NoContent();
});



//----------------------------------------------------------------------------------------------------------
//Partial Update student JsonPatch Method

//----------------------------------------------------------
app.MapPatch("/api/students/{id:int}", async (int id, JsonPatchDocumentDTO patchDocument, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var student = dbContext.Students.Find(id);

    if (student == null)
    {
        return Results.NotFound("Student not found.");
    }

    foreach (var operation in patchDocument.Operations)
    {
        if (operation.Op == "replace")
        {
            var property = operation.Path.TrimStart('/'); // Remove the leading '/'
            var value = operation.Value;

            // Find the corresponding property on the 'student' object
            var studentProperty = typeof(Student).GetProperty(property);

            if (studentProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, studentProperty.PropertyType);

                    // Apply the patch operation
                    studentProperty.SetValue(student, typedValue);
                }

                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }

                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the patch operation. Error: {ex.Message}");
                }
            }

            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Student' object.");
            }
        }

        else if (operation.Op == "add")
        {
            var property = operation.Path.TrimStart('/');
            var value = operation.Value;

            // Find the corresponding property on the 'student' object
            var studentProperty = typeof(Student).GetProperty(property);

            if (studentProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, studentProperty.PropertyType);
                    // Check if the property value is null
                    if (typedValue == null)
                    {
                        return Results.BadRequest("Value cannot be null for the 'add' operation.");
                    }

                    // Apply the 'add' operation
                    studentProperty.SetValue(student, typedValue);
                }

                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }

                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
                }


            }

            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Student' object.");
            }

        }

        else if (operation.Op == "remove")
        {
            // Handle 'remove' operation
            var property = operation.Path.TrimStart('/');
            var studentProperty = typeof(Student).GetProperty(property);

            if (studentProperty != null)
            {
                // Set the property value to null to remove it
                studentProperty.SetValue(student, null);
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Student' object.");
            }
        }
    }

    // Save changes to the database
    dbContext.SaveChanges();

    // Update the cache with the updated data
    var cacheKey = "students";

   

// Fetch the existing cached students
var existingCachedStudents = await cache.GetStringAsync(cacheKey);
    if (existingCachedStudents != null)
    {
        List<Student> studentsToCache = JsonSerializer.Deserialize<List<Student>>(existingCachedStudents);

        // Find and remove the deleted student from the cached list
        studentsToCache.RemoveAll(s => s.StudentId == id);

        // Serialize and update the cached list in the cache
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(studentsToCache), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
        });
    }

    return Results.NoContent();


});

//----------------------------------------------------------





//---------------------------------------------

//delete student
app.MapDelete("/api/students/{id:int}", async (int id, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var student = dbContext.Students.Find(id);
    if (student == null)
    {
        return Results.NotFound();
    }

    // Store the deleted student in a separate cache key before removal
    var deletedCacheKey = "deleted_students";
    var deletedStudentsList = await cache.GetStringAsync(deletedCacheKey) ?? "[]";
    var deletedStudents = JsonSerializer.Deserialize<List<Student>>(deletedStudentsList);
    deletedStudents.Add(student);
    await cache.SetStringAsync(deletedCacheKey, JsonSerializer.Serialize(deletedStudents));
    //-----------------------------------------------------------------------

    dbContext.Students.Remove(student);
    await dbContext.SaveChangesAsync();

    // Remove the student from the cache
    var cacheKey = "students";

    // Fetch the existing cached students
    var existingCachedStudents = await cache.GetStringAsync(cacheKey);
    if (existingCachedStudents != null)
    {
        List<Student> studentsToCache = JsonSerializer.Deserialize<List<Student>>(existingCachedStudents);

        // Find and remove the deleted student from the cached list
        studentsToCache.RemoveAll(s => s.StudentId == id);

        // Serialize and update the cached list in the cache
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(studentsToCache), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
        });
    }

    return Results.NoContent();
});
//---------------------------------------------------------------------------------------------------
//-----------------------------------Endpoints for Classes-------------------------------------------
//Get all the Classes
/*app.MapGet("/api/Classes", (StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.ToList();
    return Results.Ok(classes);
});*/

app.MapGet("/api/classes", async (StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var cacheKey = "classes"; // Cache key for storing Classes data

    // Try to get Classes from the cache
    var cachedClasses = await cache.GetStringAsync(cacheKey);
    if (cachedClasses != null)
    {
        // If data is found in the cache, return it
        var classes = JsonSerializer.Deserialize<List<Class>>(cachedClasses);
        return Results.Ok(classes);
    }

    // If data is not in the cache, fetch from the database
    var classesFromDb = dbContext.Classes.ToList();

    // Serialize and store Classes data in the cache for a specified duration
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(classesFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Ok(classesFromDb);
});


//-----------------------------------------------------------------------------------

//Get classes by id
/*app.MapGet("/api/Classes/{id:int}", (int id, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);
    if (classes == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(classes);
});*/

app.MapGet("/api/classes/{id:int}", async (int id, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var cacheKey = "class_" + id; // Cache key for storing a specific class's data

    // Try to get the class from the cache
    var cachedClass = await cache.GetStringAsync(cacheKey);
    if (cachedClass != null)
    {
        // If data is found in the cache, return it
        var classData = JsonSerializer.Deserialize<Class>(cachedClass);
        return Results.Ok(classData);
    }

    // If data is not in the cache, fetch from the database
    var classFromDb = dbContext.Classes.Find(id);

    if (classFromDb == null)
    {
        return Results.NotFound();
    }

    // Serialize and store the class data in the cache for a specified duration
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(classFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Ok(classFromDb);
});


//-----------------------------------------------------------------------------------------------

//post classes 
/*app.MapPost("/api/Classes", async (Class classes, StudentAPIDBcontext dbContext) =>
{
    dbContext.Classes.Add(classes);
    await dbContext.SaveChangesAsync();
    return Results.Created($"/api/Classes/{classes.ClassId}", classes);
});*/

app.MapPost("/api/classes", async (Class classes, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    // Add the class to the database
    dbContext.Classes.Add(classes);
    await dbContext.SaveChangesAsync();

    // Serialize and store the class data in the cache for a specified duration
    var cacheKey = "class_" + classes.ClassId; // Cache key specific to the class's Id
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(classes), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Created($"/api/classes/{classes.ClassId}", classes);
});


//-------------------------------------------------------------------------------------------------

//update classes
/*app.MapPut("/api/Classes/{id:int}", async (int id, Class updatedClass, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);
    if (classes == null)
    {
        return Results.NotFound();
    }

    classes.ClassName = updatedClass.ClassName;
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});*/

app.MapPut("/api/classes/{id:int}", async (int id, Class updatedClass, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var classData = dbContext.Classes.Find(id);
    if (classData == null)
    {
        return Results.NotFound();
    }

    // Update the class data in the database
    classData.ClassName = updatedClass.ClassName;
    await dbContext.SaveChangesAsync();

    // Update the cache with the updated data
    var cacheKey = "class_" + id; // Cache key specific to the class's Id

    // Serialize and update the cached class data in the cache
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(classData), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.NoContent();
});




//--------------------------------------------------------------------------------------------

//delete student
/*app.MapDelete("/api/Classes/{id:int}", async (int id, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);
    if (classes == null)
    {
        return Results.NotFound();
    }

    dbContext.Classes.Remove(classes);
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});*/

app.MapDelete("/api/classes/{id:int}", async (int id, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var classData = dbContext.Classes.Find(id);
    if (classData == null)
    {
        return Results.NotFound();
    }

    // Serialize the class data
    var serializedClassData = JsonSerializer.Serialize(classData);

    // Remove the class from the database
    dbContext.Classes.Remove(classData);
    await dbContext.SaveChangesAsync();

    // Remove the class from the Redis cache after a delay (e.g., 10 minutes)
    var cacheKey = "class_" + id; // Cache key specific to the class's Id
    await cache.SetStringAsync(cacheKey, serializedClassData, new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.NoContent();
});



//----------------------------------------------------------------------------------------

//Jsonpatch patch method for Classes

/*app.MapPatch("/api/Classes/{id:int}", async (int id, JsonPatchDocumentDTO patchDocument, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);

    if (classes == null)
    {
        return Results.NotFound("Class not found.");
    }

    foreach (var operation in patchDocument.Operations)
    {
        if (operation.Op == "replace")
        {
            var property = operation.Path.TrimStart('/'); // Remove the leading '/'
            var value = operation.Value;

            // Find the corresponding property on the 'student' object
            var classProperty = typeof(Class).GetProperty(property);

            if (classProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, classProperty.PropertyType);

                    // Apply the patch operation
                    classProperty.SetValue(classes, typedValue);
                }

                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }

                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the patch operation. Error: {ex.Message}");
                }
            }

            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
            }
        }

        else if (operation.Op == "add")
        {
            var property = operation.Path.TrimStart('/');
            var value = operation.Value;

            // Find the corresponding property on the 'student' object
            var classProperty = typeof(Class).GetProperty(property);

            if (classProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, classProperty.PropertyType);
                    // Check if the property value is null
                    if (typedValue == null)
                    {
                        return Results.BadRequest("Value cannot be null for the 'add' operation.");
                    }

                    // Apply the 'add' operation
                    classProperty.SetValue(classes, typedValue);
                }

                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }

                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
                }


            }

            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
            }

        }

        else if (operation.Op == "remove")
        {
            // Handle 'remove' operation
            var property = operation.Path.TrimStart('/');
            var classProperty = typeof(Class).GetProperty(property);

            if (classProperty != null)
            {
                // Set the property value to null to remove it
                classProperty.SetValue(classes, null);
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
            }
        }
    }

    // Save changes to the database
    dbContext.SaveChanges();

    return Results.NoContent();


});*/

app.MapPatch("/api/classes/{id:int}", async (int id, JsonPatchDocumentDTO patchDocument, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var classData = dbContext.Classes.Find(id);

    if (classData == null)
    {
        return Results.NotFound("Class not found.");
    }

    foreach (var operation in patchDocument.Operations)
    {
        if (operation.Op == "replace")
        {
            var property = operation.Path.TrimStart('/'); // Remove the leading '/'
            var value = operation.Value;

            // Find the corresponding property on the 'class' object
            var classProperty = typeof(Class).GetProperty(property);

            if (classProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, classProperty.PropertyType);

                    // Apply the patch operation
                    classProperty.SetValue(classData, typedValue);
                }
                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the patch operation. Error: {ex.Message}");
                }
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
            }
        }
        else if (operation.Op == "add")
        {
            var property = operation.Path.TrimStart('/');
            var value = operation.Value;

            // Find the corresponding property on the 'class' object
            var classProperty = typeof(Class).GetProperty(property);

            if (classProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, classProperty.PropertyType);
                    // Check if the property value is null
                    if (typedValue == null)
                    {
                        return Results.BadRequest("Value cannot be null for the 'add' operation.");
                    }

                    // Apply the 'add' operation
                    classProperty.SetValue(classData, typedValue);
                }
                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
                }
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
            }
        }
        else if (operation.Op == "remove")
        {
            var property = operation.Path.TrimStart('/');
            var classProperty = typeof(Class).GetProperty(property);

            if (classProperty != null)
            {
                // Set the property value to null to remove it
                classProperty.SetValue(classData, null);
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
            }
        }
    }

    // Save changes to the database
    dbContext.SaveChanges();

    // Update the cache with the updated class data
    var cacheKey = "class_" + id; // Cache key specific to the class's Id

    // Serialize and update the cached class data in the cache
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(classData), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.NoContent();
});



//------------------------------------------------------------------------------

//--------------------------Endpoints for Attendance

//Get all the Attendance
/*app.MapGet("/api/Attendance", (StudentAPIDBcontext dbContext) =>
{
    var attendance = dbContext.Attendances.ToList();
    return Results.Ok(attendance);
});*/

app.MapGet("/api/attendance", async (StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var cacheKey = "attendance";

    // Try to get attendance data from the cache
    var cachedAttendance = await cache.GetStringAsync(cacheKey);
    if (cachedAttendance != null)
    {
        // If data is found in the cache, return it
        var attendance = JsonSerializer.Deserialize<List<Attendance>>(cachedAttendance);
        return Results.Ok(attendance);
    }

    // If data is not in the cache, fetch from the database
    var attendanceFromDb = dbContext.Attendances.ToList();

    // Serialize and store attendance data in the cache for a specified duration
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(attendanceFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Ok(attendanceFromDb);
});


//------------------------------------------------------------------------------------------------
//Get Attendance by id
/*app.MapGet("/api/Attendance/{StudentId:int}/{ClassId:int}", ([FromRoute] int StudentId, [FromRoute] int ClassId, StudentAPIDBcontext dbContext) =>
{
    var attendance = dbContext.Attendances.Find(StudentId, ClassId);
    if (attendance == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(attendance);

});*/

app.MapGet("/api/attendance/{StudentId:int}/{ClassId:int}", async ([FromRoute] int StudentId, [FromRoute] int ClassId, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var cacheKey = $"attendance_{StudentId}_{ClassId}";

    // Try to get attendance data by StudentId and ClassId from the cache
    var cachedAttendance = await cache.GetStringAsync(cacheKey);
    if (cachedAttendance != null)
    {
        // If data is found in the cache, return it
        var attendance = JsonSerializer.Deserialize<Attendance>(cachedAttendance);
        return Results.Ok(attendance);
    }

    // If data is not in the cache, fetch from the database
    var attendanceFromDb = dbContext.Attendances.Find(StudentId, ClassId);

    if (attendanceFromDb == null)
    {
        return Results.NotFound();
    }

    // Serialize and store attendance data in the cache for a specified duration
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(attendanceFromDb), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.Ok(attendanceFromDb);
});




//--------------------------------------------------------------------------------------------------

//post Attendance 
/*app.MapPost("/api/Attendance", async (Attendance attendances, StudentAPIDBcontext dbContext) =>
{
    //dbContext.Attendances.Add(attendances);
    //await dbContext.SaveChangesAsync();
    //return Results.Created($"/api/Attendance/{attendances.AttendanceId}", attendances);

    try
    {
        // Check if the attendance already exists based on some criteria (e.g., time, student, class)
        var existingAttendance = await dbContext.Attendances.FirstOrDefaultAsync(a =>
            a.Time == attendances.Time && a.StudentId == attendances.StudentId && a.ClassId == attendances.ClassId);

        if (existingAttendance == null)
        {
            // Attendance doesn't exist, so add it to the database
            dbContext.Attendances.Add(attendances);
        }
        else
        {
            // Attendance exists, attach it to the context
            dbContext.Attendances.Attach(existingAttendance);
            // Ensure the AttendanceId is set
            attendances.AttendanceId = existingAttendance.AttendanceId;
        }

        await dbContext.SaveChangesAsync();

        // Return a success message or status
        return Results.Ok("Attendance Added successfully.");

        // Return the created or existing attendance
        return TypedResults.Created($"/attendances/{attendances.AttendanceId}", attendances);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to create attendance. Error: {ex.Message}");
    }

});*/

app.MapPost("/api/attendance", async (Attendance attendances, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    try
    {
        // Check if the attendance already exists based on some criteria (e.g., time, student, class)
        var existingAttendance = await dbContext.Attendances.FirstOrDefaultAsync(a =>
            a.Time == attendances.Time && a.StudentId == attendances.StudentId && a.ClassId == attendances.ClassId);

        if (existingAttendance == null)
        {
            // Attendance doesn't exist, so add it to the database
            dbContext.Attendances.Add(attendances);
        }
        else
        {
            // Attendance exists, attach it to the context
            dbContext.Attendances.Attach(existingAttendance);
            // Ensure the AttendanceId is set
            attendances.AttendanceId = existingAttendance.AttendanceId;
        }

        await dbContext.SaveChangesAsync();

        // Return a success message or status
        var result = Results.Ok("Attendance Added successfully.");

        // Store the attendance data in the Redis cache
        var cacheKey = $"attendance_{attendances.StudentId}_{attendances.ClassId}";
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(attendances), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
        });

        return result;
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to create attendance. Error: {ex.Message}");
    }
});




//------------------------------------------------------------------------------------------------

//app.MapPut("/api/Attendance/{StudentId:int}/{ClassId:int}", async ( [FromRoute]int StudentId, [FromRoute]int ClassId,  [FromBody] Attendance updatedAttendance, StudentAPIDBcontext dbContext) =>
//{
//    try
//    {
//        // Find the existing attendance by the composite key
//        var existingAttendance = await dbContext.Attendances.FindAsync( StudentId, ClassId);

//        if (existingAttendance == null)
//        {
//            // Attendance doesn't exist, return a not found error
//            return Results.NotFound("Attendance not found.");
//        }

//        // Update the properties you need to change
//        /*existingAttendance.StudentId = updatedAttendance.StudentId; // Replace with actual property names
//		existingAttendance.ClassId = updatedAttendance.ClassId; // Replace with actual property names*/
//        existingAttendance.Time = updatedAttendance.Time; // Replace with actual property names

//        await dbContext.SaveChangesAsync();

//        // Return the updated attendance
//        return TypedResults.Ok(existingAttendance);
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest($"Failed to update attendance. Error: {ex.Message}");
//    }
//});


app.MapPut("/api/attendance/{StudentId:int}/{ClassId:int}", async (int StudentId, int ClassId, Attendance updatedAttendance, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    try
    {
        // Find the existing attendance by the composite key
        var existingAttendance = await dbContext.Attendances.FindAsync(StudentId, ClassId);

        if (existingAttendance == null)
        {
            // Attendance doesn't exist, return a not found error
            return Results.NotFound("Attendance not found.");
        }

        // Update the properties you need to change
        existingAttendance.Time = updatedAttendance.Time; // Replace with actual property names

        await dbContext.SaveChangesAsync();

        // Return the updated attendance
        var result = TypedResults.Ok(existingAttendance);

        // Update the attendance data in the Redis cache
        var cacheKey = $"attendance_{StudentId}_{ClassId}";
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(existingAttendance), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
        });

        return result;
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to update attendance. Error: {ex.Message}");
    }
});



//------------------------------------------------------------------------------------------------------

//delete Attendance
app.MapDelete("/api/Attendance/{id:int}", async (int id, StudentAPIDBcontext dbContext) =>
{
    try
    {
        // Check if the attendance to be deleted exists based on the specified criteria (e.g., time, student, class)
        var existingAttendance = await dbContext.Attendances.FirstOrDefaultAsync(a =>
        a.AttendanceId == id);

        // a.Time == time && a.StudentId == studentId && a.ClassId == classId);

        if (existingAttendance == null)
        {
            // Attendance doesn't exist, return a not found error
            return Results.NotFound("Attendance not found.");
        }

        // Remove the attendance from the database
        dbContext.Attendances.Remove(existingAttendance);
        await dbContext.SaveChangesAsync();

        // Return a success message or status
        return Results.Ok("Attendance deleted successfully.");


    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to delete attendance. Error: {ex.Message}");
    }
});


app.MapDelete("/api/attendance/{StudentId:int}/{ClassId:int}", async (int StudentId, int ClassId, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    try
    {
        // Check if the attendance to be deleted exists based on its composite keys
        var existingAttendance = await dbContext.Attendances
            .Where(a => a.StudentId == StudentId && a.ClassId == ClassId)
            .FirstOrDefaultAsync();

        if (existingAttendance == null)
        {
            // Attendance doesn't exist, return a not found error
            return Results.NotFound("Attendance not found.");
        }

        // Remove the attendance from the database
        dbContext.Attendances.Remove(existingAttendance);
        await dbContext.SaveChangesAsync();

        // Cache the deleted attendance with a specific key and a short expiration
        var cacheKey = $"attendance_deleted_{StudentId}_{ClassId}";
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(existingAttendance), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
        });

        await cache.RemoveAsync(cacheKey);

        // Return a success message or status
        return Results.Ok("Attendance deleted successfully.");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to delete attendance. Error: {ex.Message}");
    }
});









//--------------------------------------JsonPatch ---------------------------------------------------------
/*app.MapPatch("/api/Attendance/{StudentId:int}/{ClassId:int}", async ([FromRoute] int StudentId, [FromRoute] int ClassId, JsonPatchDocumentDTO patchDocument, StudentAPIDBcontext dbContext) =>
{
    var existingAttendance = await dbContext.Attendances.FindAsync(StudentId, ClassId);

    if (existingAttendance == null)
    {
        return Results.NotFound("Attendance  not found.");
    }

    foreach (var operation in patchDocument.Operations)
    {
        if (operation.Op == "replace")
        {
            var property = operation.Path.TrimStart('/'); // Remove the leading '/'
            var value = operation.Value;

            // Find the corresponding property on the 'student' object
            var attendanceProperty = typeof(Attendance).GetProperty(property);

            if (attendanceProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, attendanceProperty.PropertyType);

                    // Apply the patch operation
                    attendanceProperty.SetValue(existingAttendance, typedValue);
                }

                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }

                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the patch operation. Error: {ex.Message}");
                }
            }

            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
            }
        }

        else if (operation.Op == "add")
        {
            var property = operation.Path.TrimStart('/');
            var value = operation.Value;

            // Find the corresponding property on the 'student' object
            var attendanceProperty = typeof(Attendance).GetProperty(property);

            if (attendanceProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, attendanceProperty.PropertyType);
                    // Check if the property value is null
                    if (typedValue == null)
                    {
                        return Results.BadRequest("Value cannot be null for the 'add' operation.");
                    }

                    // Apply the 'add' operation
                    attendanceProperty.SetValue(existingAttendance, typedValue);
                }

                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }

                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
                }


            }

            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
            }

        }

        else if (operation.Op == "remove")
        {
            // Handle 'remove' operation
            var property = operation.Path.TrimStart('/');
            var attendanceProperty = typeof(Attendance).GetProperty(property);

            if (attendanceProperty != null)
            {
                // Set the property value to null to remove it
                attendanceProperty.SetValue(existingAttendance, null);
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
            }
        }
    }

    // Save changes to the database
    dbContext.SaveChanges();

    return Results.NoContent();


});*/

app.MapPatch("/api/attendance/{StudentId:int}/{ClassId:int}", async ([FromRoute] int StudentId, [FromRoute] int ClassId, JsonPatchDocumentDTO patchDocument, StudentAPIDBcontext dbContext, IDistributedCache cache) =>
{
    var existingAttendance = await dbContext.Attendances.FindAsync(StudentId, ClassId);

    if (existingAttendance == null)
    {
        return Results.NotFound("Attendance not found.");
    }

    foreach (var operation in patchDocument.Operations)
    {
        if (operation.Op == "replace" || operation.Op == "add")
        {
            var property = operation.Path.TrimStart('/'); // Remove the leading '/'
            var value = operation.Value;

            // Find the corresponding property on the 'attendance' object
            var attendanceProperty = typeof(Attendance).GetProperty(property);

            if (attendanceProperty != null)
            {
                try
                {
                    // Convert the 'value' to the correct data type for the property
                    var typedValue = Convert.ChangeType(value, attendanceProperty.PropertyType);

                    // Apply the patch operation
                    attendanceProperty.SetValue(existingAttendance, typedValue);
                }
                catch (InvalidCastException)
                {
                    // Handle invalid data type conversion
                    return Results.BadRequest("Invalid data type for the property.");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions that may occur
                    return Results.BadRequest($"Failed to apply the patch operation. Error: {ex.Message}");
                }
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
            }
        }
        else if (operation.Op == "remove")
        {
            // Handle 'remove' operation
            var property = operation.Path.TrimStart('/');
            var attendanceProperty = typeof(Attendance).GetProperty(property);

            if (attendanceProperty != null)
            {
                // Set the property value to null to remove it
                attendanceProperty.SetValue(existingAttendance, null);
            }
            else
            {
                // Handle the case where the property doesn't exist
                return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
            }
        }
    }

    // Save changes to the database
    dbContext.SaveChanges();

    // Update the cache with the updated attendance data
    var cacheKey = $"attendance_{StudentId}_{ClassId}";

    // Serialize and update the cached attendance data in the cache
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(existingAttendance), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // Cache expiration time (e.g., 10 minutes)
    });

    return Results.NoContent();
});





app.Run();















