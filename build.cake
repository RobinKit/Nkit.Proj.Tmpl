#tool "nuget:?package=Cake.CoreCLR";
 
#tool "nuget:?package=xunit.runner.console&version=2.3.0-beta4-build3742"
#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NuGet.CommandLine"
 
#addin "Cake.FileHelpers"
#addin "nuget:?package=NuGet.Core"
#addin "nuget:?package=Cake.ExtendedNuGet"
#addin "nuget:?package=Cake.Incubator"

using NuGet; 
using Cake.Common.Solution;
using Cake.Incubator;
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var projectName = "Nkit.Proj.Tmpl";
var solutionFile = "./" + projectName + ".sln";

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var toolpath = Argument("toolpath", @"tools");
var artifactsPath = Argument("artifactPath", @"artifacts");
var branch = Argument("branch", EnvironmentVariable("APPVEYOR_REPO_BRANCH"));
var nugetApiKey = EnvironmentVariable("nugetApiKey");
var solution= ParseSolution(solutionFile);
var projects=solution.Projects;

var nupkgPath = $"{artifactsPath}/nupkg";
var nupkgRegex = $"**/{projectName}*.nupkg";
var nugetPath = toolpath + "/nuget.exe";
var nugetQueryUrl = "https://www.nuget.org/api/v2/";
var nugetPushUrl = "https://www.nuget.org/api/v2/package";
var NUGET_PUSH_SETTINGS = new NuGetPushSettings
                          {
                              ToolPath = File(nugetPath),
                              Source = nugetPushUrl,
                              ApiKey = nugetApiKey
                          };

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        Information("Current Branch is:" + EnvironmentVariable("APPVEYOR_REPO_BRANCH"));
        CleanDirectories("./src/**/bin");
        CleanDirectories("./src/**/obj");
		CleanDirectories("./test/**/bin");
		CleanDirectories("./test/**/obj");
        CleanDirectory(nupkgPath);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore(solutionFile);
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        MSBuild(solutionFile, new MSBuildSettings(){Configuration = configuration}
                                               .WithProperty("SourceLinkCreate","true"));
    });
	Task("test-sln-parser")
	.Does(()=>{
	    var testprojects =  solution.Projects
	 								   .Where(p=>p.Name.EndsWith(".Tests"));
		 							   Information(projects.Count());
									   foreach(var i in testprojects)
									   {
									    Information(i.Name+"   ___"+i.Path.FullPath);
									   }
                                     
	});
Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
    {
 
       var projects =  solution.Projects
	 								   .Where(p=>p.Name.EndsWith(".Tests"));
		 							   Information(projects.Count());
		 foreach(var i in projects)
		  {
			  Information(i.Name+"   ___"+i.Path.FullPath);
     DotNetCoreTest(i.Path.FullPath, 
	      new DotNetCoreTestSettings { Configuration = "Debug"}); 
		 }				   
           
    });
    
Task("Pack")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
    {
       // var nupkgFiles = GetFiles(nupkgRegex);
      //  MoveFiles(nupkgFiles, nupkgPath);
    });

Task("NugetPublish")
    .IsDependentOn("Pack")
    .WithCriteria(() => branch == "master")
    .Does(()=>
    {
        foreach(var nupkgFile in GetFiles(nupkgRegex))
        {
          if(!IsNuGetPublished(nupkgFile, nugetQueryUrl))
          {
             Information("Publishing... " + nupkgFile);
             NuGetPush(nupkgFile, NUGET_PUSH_SETTINGS);
          }
          else
          {
             Information("Already published, skipping... " + nupkgFile);
          }
        }
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Pack")
    .IsDependentOn("NugetPublish");
    
//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
