// Global usings shared by all test classes (previously the header of Tests.cs).
global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.IO;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Extensions.DependencyInjection;
global using WingetStore.Models;
global using WingetStore.Services;
global using WingetStore.ViewModels;
global using Microsoft.UI.Dispatching;
global using Microsoft.UI.Xaml;
global using WingetStore.Pages;
global using Xunit;
global using CommunityToolkit.Mvvm.Messaging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

