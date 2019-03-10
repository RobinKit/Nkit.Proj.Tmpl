#tool "nuget:?package=Cake.CoreCLR";
 
#tool "nuget:?package=xunit.runner.console&version=2.3.0-beta4-build3742"
#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NuGet.CommandLine"
 
#addin "Cake.FileHelpers"
#addin "nuget:?package=NuGet.Core"
#addin "nuget:?package=Cake.ExtendedNuGet"
#addin "nuget:?package=Cake.Incubator"
#addin nuget:?package=Cake.Git


using NuGet; 
using Cake.Common.Solution;
using Cake.Incubator;
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var isLocalBuild        = !AppVeyor.IsRunningOnAppVeyor;
var projectName = "Nkit.Proj.Tmpl";
var solutionFile = "./" + projectName + ".sln";

var solutionRootPath = MakeAbsolute(Directory("."));
var rootPath = MakeAbsolute(Directory("."));

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var toolpath = Argument("toolpath", @"tools");
var version =Argument("version", "1.0.2");

var artifactsPath = Argument("artifactPath", @"artifacts");
var branch = Argument("branch", EnvironmentVariable("APPVEYOR_REPO_BRANCH"));

var solution= ParseSolution(solutionFile);
var projects=solution.Projects; 
var lastCommit = GitLogTip("./");
//////////////////////////////////////////////////////////////////////
// Repository Git Info
//////////////////////////////////////////////////////////////////////
Information(@"Last commit {0}
    Short message: {1}
    Author:        {2}
    Authored:      {3:yyyy-MM-dd HH:mm:ss}
    Committer:     {4}
    Committed:     {5:yyyy-MM-dd HH:mm:ss}",
    lastCommit.Sha,
    lastCommit.MessageShort,
    lastCommit.Author.Name,
    lastCommit.Author.When,
    lastCommit.Committer.Name,
    lastCommit.Committer.When
    );
var nupkgPath = $"{artifactsPath}/nupkg";
var nupkgRegex = $"**/{projectName}*.nupkg";
var nugetPath = toolpath + "/nuget.exe";
var nugetQueryUrl = "https://www.nuget.org/api/v2/";
var nugetPushUrl = isLocalBuild?"https://www.nuget.org/api/v2/package" : EnvironmentVariable("nugetServerUrl");
var nugetApiKey = EnvironmentVariable("nugetApiKey");
var NUGET_PUSH_SETTINGS = new NuGetPushSettings
                          {
                              ToolPath = File(nugetPath),
                              Source = nugetPushUrl,
                              ApiKey = nugetApiKey
                          };

 var semVersion          = isLocalBuild
                                ? version
                                : string.Concat(version, "-build-", AppVeyor.Environment.Build.Number.ToString("0000"));

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        Information("Current Branch is:" + EnvironmentVariable("APPVEYOR_REPO_BRANCH")); 
        CleanDirectories("./**/**/obj");
		CleanDirectories("./**/**/bin"); 
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
                                               .WithProperty("SourceLinkCreate","true"))
											   //.WithProperty("PackageOutputPath",nugetPath)
											   ;
    });
Task("test-sln-parser")
	.Does(()=>{
	    var testprojects  =  solution.Projects
	 								   .Where(p=>!p.Name.EndsWith(".Tests")&& p.Path.FullPath.EndsWith(".csproj"));
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
	 								   .Where(p=>p.Name.EndsWith(".Tests")&& p.Path.FullPath.EndsWith(".csproj"));
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
       var packprojects =  solution.Projects
	 								   .Where(p=>!p.Name.EndsWith(".Tests") && p.Path.FullPath.EndsWith(".csproj"));
	  Information(projects.Count());
	  var cfg= new DotNetCorePackSettings
        {
            Configuration = configuration,
            MSBuildSettings = new DotNetCoreMSBuildSettings().SetVersion(semVersion),
            NoBuild = true,
            OutputDirectory = Directory(nupkgPath)
        };
	  foreach(var i in packprojects)
		  {
			  Information("Pack:"+i.Name);
        DotNetCorePack(i.Path.FullPath,cfg); 
		 }		
    });

Task("NugetPublish")
.IsDependentOn("Pack")
   // .WithCriteria(() => branch == "master")
    .Does(()=>
    {
	
		Information($"{rootPath}/{nupkgPath}");
        foreach(var nupkgFile in System.IO.Directory.GetFiles($"{rootPath}/{nupkgPath}","*.nupkg"))
        {
		    Information(nupkgFile);
          if(!IsNuGetPublished(nupkgFile,version:semVersion, nugetSource:nugetQueryUrl))
          {
             Information("Publishing... " + nupkgFile);
             NuGetPush(nupkgFile, NUGET_PUSH_SETTINGS);
          }
          else
          {
             Information("Already published, skipping... " + nupkgFile);
          }
        }
    }).OnError(ex =>
{
	Information(ex.Message);
    // Handle the error here.
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
