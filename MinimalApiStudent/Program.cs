//using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using FluentAssertions.Common;
using MinimalApiStudent;
using MinimalApiStudent.Models;
using Polly;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<StudentAPIDBcontext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnectionNew"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
//--------------------------------Endpoints for students-------------------------------------------
//Get all the students
app.MapGet("/api/students", (StudentAPIDBcontext dbContext) =>
{
    var students = dbContext.Students.ToList();
    return Results.Ok(students);
});

//Get students by id
app.MapGet("/api/students/{id:int}", (int id, StudentAPIDBcontext dbContext) =>
{
    var student = dbContext.Students.Find(id);
    if (student == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(student);
});

//post student 
app.MapPost("/api/students", async (Student student, StudentAPIDBcontext dbContext) =>
{
    dbContext.Students.Add(student);
    await dbContext.SaveChangesAsync();
    return Results.Created($"/api/students/{student.StudentId}", student);
});

//update student
app.MapPut("/api/students/{id:int}", async (int id, Student updatedStudent, StudentAPIDBcontext dbContext) =>
{
    var student = dbContext.Students.Find(id);
    if (student == null)
    {
        return Results.NotFound();
    }

    student.StudentName = updatedStudent.StudentName;
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});

//delete student
app.MapDelete("/api/students/{id:int}", async (int id, StudentAPIDBcontext dbContext) =>
{
    var student = dbContext.Students.Find(id);
    if (student == null)
    {
        return Results.NotFound();
    }

    dbContext.Students.Remove(student);
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});
//---------------------------------------------------------------------------------------------------
//-----------------------------------Endpoints for Classes-------------------------------------------
//Get all the Classes
app.MapGet("/api/Classes", (StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.ToList();
    return Results.Ok(classes);
});

//Get classes by id
app.MapGet("/api/Classes/{id:int}", (int id, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);
    if (classes == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(classes);
});

//post classes 
app.MapPost("/api/Classes", async (Class classes, StudentAPIDBcontext dbContext) =>
{
    dbContext.Classes.Add(classes);
    await dbContext.SaveChangesAsync();
    return Results.Created($"/api/Classes/{classes.ClassId}", classes);
});

//update classes
app.MapPut("/api/Classes/{id:int}", async (int id, Class updatedClass, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);
    if (classes == null)
    {
        return Results.NotFound();
    }

    classes.ClassName = updatedClass.ClassName;
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});

//delete student
app.MapDelete("/api/Classes/{id:int}", async (int id, StudentAPIDBcontext dbContext) =>
{
    var classes = dbContext.Classes.Find(id);
    if (classes == null)
    {
        return Results.NotFound();
    }

    dbContext.Classes.Remove(classes);
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
});
//----------------------------------------------------------------------------------------------
//--------------------------Endpoints for Attendance

//Get all the Attendance
app.MapGet("/api/Attendance", (StudentAPIDBcontext dbContext) =>
{
    var attendance = dbContext.Attendances.ToList();
    return Results.Ok(attendance);
});

//Get Attendance by id
app.MapGet("/api/Attendance/{id:int}", (int id, StudentAPIDBcontext dbContext) =>
{
    var attendance = dbContext.Attendances.Find(id);
    if (attendance == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(attendance);

});
//--------------------------------------------------------------------------------------------------

//post Attendance 
app.MapPost("/api/Attendance", async (Attendance attendances, StudentAPIDBcontext dbContext) =>
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

});

//------------------------------------------------------------------------------------------------

app.MapPut("/api/Attendance/{StudentId:int}/{ClassId:int}", async ( [FromRoute]int StudentId, [FromRoute]int ClassId,  [FromBody] Attendance updatedAttendance, StudentAPIDBcontext dbContext) =>
{
    try
    {
        // Find the existing attendance by the composite key
        var existingAttendance = await dbContext.Attendances.FindAsync( StudentId, ClassId);

        if (existingAttendance == null)
        {
            // Attendance doesn't exist, return a not found error
            return Results.NotFound("Attendance not found.");
        }

        // Update the properties you need to change
        /*existingAttendance.StudentId = updatedAttendance.StudentId; // Replace with actual property names
		existingAttendance.ClassId = updatedAttendance.ClassId; // Replace with actual property names*/
        existingAttendance.Time = updatedAttendance.Time; // Replace with actual property names

        await dbContext.SaveChangesAsync();

        // Return the updated attendance
        return TypedResults.Ok(existingAttendance);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to update attendance. Error: {ex.Message}");
    }
});


//------------------------------------------------------------------------------------------------------

//delete Attendance
app.MapDelete("/api/Attendance/{id:int}", async (int id,  StudentAPIDBcontext dbContext) =>
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







//-----------------------------------------------------------------------------------------------

//post student data 
//app.MapPost("/api/students", async (Student student, StudentAPIDBcontext dbContext) =>
//{
//    //dbContext.Students.Add(student);
//    //await dbContext.SaveChangesAsync();
//    //return Results.Created($"/api/students/{student.StudentId}", student);

//    try
//    {

//        // Check if the student already exists by name
//        var existingStudent = await dbContext.Students.FirstOrDefaultAsync(s => s.StudentName == student.StudentName);
//        if (existingStudent == null)
//        {
//            // Student doesn't exist, so add them to the database
//            dbContext.Students.Add(student);
//        }
//        else
//        {
//            // Student exists, attach it to the context
//            dbContext.Students.Attach(existingStudent);
//            student.StudentId = existingStudent.StudentId; // Ensure the StudentId is set
//        }
//        await dbContext.SaveChangesAsync();

//        // Return the created or existing student
//        return TypedResults.Created($"/students/{student.StudentId}", student);
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest($"Failed to create student. Error: {ex.Message}");
//    }
//});






app.Run();


