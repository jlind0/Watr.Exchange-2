﻿using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Watr.Exchange.Security.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using DynamicData.Binding;
using System.Reactive.Disposables;
namespace Watr.Exchange.ViewModels
{
    public class MainViewModel : ReactiveObject, IRoutableViewModel
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        public string? UrlPathSegment => "main";
        protected ISecurityProvider SecurityProvider { get; }
        public IScreen HostScreen { get; }
        private bool isLoggedIn;
        public bool IsLoggedIn
        {
            get => isLoggedIn;
            set => this.RaiseAndSetIfChanged(ref isLoggedIn, value);
        }
        private string? userName;
        public string? UserName
        {
            get => userName;
            set => this.RaiseAndSetIfChanged(ref userName, value);
        }
        public ReactiveCommand<Unit, Unit> Login { get; }
        public ReactiveCommand<Unit, Unit> Logout { get; }
        protected ILogger Logger { get; }
        protected string[] Scopes { get; }
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        public MainViewModel(IScreen screen, ISecurityProvider security,
            ILogger<MainViewModel> logger, IConfiguration config)
        {
            HostScreen = screen;
            SecurityProvider = security;
            IsLoggedIn = security.IsAuthenticated;
            UserName = security.UserName;
            Logger = logger;
            Login = ReactiveCommand.CreateFromTask(DoLogin);
            Logout = ReactiveCommand.CreateFromTask(DoLogout);
            Scopes = config.GetSection("Scopes")?.Get<string[]>() ?? throw new InvalidDataException();
            SecurityProvider.WhenPropertyChanged(p => p.IsAuthenticated).Subscribe(p =>
            {
                IsLoggedIn = p.Sender.IsAuthenticated;
                UserName = p.Sender.UserName;
            }).DisposeWith(disposables);
        }
        ~MainViewModel()
        {
            disposables.Dispose();
        }
        protected async Task DoLogin(CancellationToken token)
        {
            try
            {
                IsLoggedIn = SecurityProvider.IsAuthenticated;
                if (IsLoggedIn)
                    return;
                await SecurityProvider.Login(Scopes, token: token);
                IsLoggedIn = SecurityProvider.IsAuthenticated;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
            finally
            {
                UserName = SecurityProvider.UserName;
            }
        }
        protected async Task DoLogout(CancellationToken token)
        {
            try
            {
                if (IsLoggedIn) await SecurityProvider.Logout(token);
                IsLoggedIn = SecurityProvider.IsAuthenticated;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
            finally
            {
                UserName = SecurityProvider.UserName;
            }
        }   
    }
    public class AppHostViewModel : ReactiveObject, IScreen
    {
        public RoutingState Router { get; } = new();
        protected ILogger Logger { get; }
        public AppHostViewModel(IServiceProvider serviceProvider, 
            ILogger<AppHostViewModel> hostLogger, IConfiguration config)
        {
            Logger = hostLogger;
            try
            {
                var scope = serviceProvider.CreateScope().ServiceProvider;
                Router.Navigate.Execute(new MainViewModel(this,
                    scope.GetRequiredService<ISecurityProvider>()
                    , scope.GetRequiredService<ILogger<MainViewModel>>(), config)).Subscribe();
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.Message, ex);
                throw;
            }
            
        }
    }
}
