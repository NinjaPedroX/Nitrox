using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using Nitrox.Launcher.ViewModels;
using Nitrox.Launcher.ViewModels.Abstract;
using Nitrox.Launcher.Views;
using ReactiveUI;

namespace Nitrox.Launcher;

internal sealed class AppViewLocator : ViewLocatorBase, ReactiveUI.IViewLocator
{
    private static readonly ConcurrentDictionary<Type, RoutableViewModelBase> viewModelCache = new();
    private static MainWindow mainWindow;
    private static RoutingState mainRouter;
    public static Lazy<AppViewLocator> Instance { get; } = new(new AppViewLocator());

    public static MainWindow MainWindow
    {
        get
        {
            if (mainWindow != null)
            {
                return mainWindow;
            }

            if (Application.Current?.ApplicationLifetime is not ClassicDesktopStyleApplicationLifetime desktop)
            {
                throw new NotSupportedException("This Avalonia application is only supported on desktop environments.");
            }
            return mainWindow = (MainWindow)desktop.MainWindow;
        }
    }

    public static RoutingState MainRouter => mainRouter ??= MainWindow.ViewModel?.Router ?? throw new Exception($"Tried to get {nameof(MainRouter)} before {nameof(Launcher.MainWindow)} was initialized");

    public override ViewDefinition Locate(object viewModel)
    {
        static Type GetViewType(object viewModel) => viewModel switch
        {
            MainWindowViewModel => typeof(MainWindow),
            ErrorViewModel => typeof(ErrorModal),
            CreateServerViewModel => typeof(CreateServerModal),
            ConfirmationBoxViewModel => typeof(ConfirmationBoxModal),
            PlayViewModel => typeof(PlayView),
            ServersViewModel vm when vm.Servers.Any() => typeof(ServersView),
            ServersViewModel vm when !vm.Servers.Any() => typeof(EmptyServersView),
            ManageServerViewModel => typeof(ManageServerView),
            _ => throw new ArgumentOutOfRangeException(nameof(viewModel), viewModel, null)
        };

        // If the view type is the same as last time, return the same instance.
        Type newView = GetViewType(viewModel);
        return new ViewDefinition(newView, () => Activator.CreateInstance(newView));
    }

    public static TViewModel GetSharedViewModel<TViewModel>() where TViewModel : RoutableViewModelBase
    {
        Type key = typeof(TViewModel);
        if (viewModelCache.TryGetValue(key, out RoutableViewModelBase vm))
        {
            return (TViewModel)vm;
        }

        TViewModel viewModel = (TViewModel)key.GetConstructor(BindingFlags.Public | BindingFlags.Instance | BindingFlags.CreateInstance, new[] { typeof(IScreen) })!.Invoke(new[] { MainWindow.ViewModel });
        viewModelCache.TryAdd(typeof(TViewModel), viewModel);
        AttachListenersForViewChange(viewModel);
        return viewModel;
    }

    /// <summary>
    ///     Any conditions used in mapping method <see cref="ResolveView{T}" /> should be listened to, so the view updates when the condition changes.
    /// </summary>
    /// <remarks>
    ///     TODO: Use source generators to implement this method based on member access operations after ViewModel types in <see cref="ResolveView{T}" />.
    /// </remarks>
    private static void AttachListenersForViewChange<T>(T viewModel)
    {
        switch (viewModel)
        {
            case ServersViewModel vm:
                vm.Servers.PropertyChanged += (_, _) => MainRouter.Navigate.Execute(vm);
                break;
        }
    }

    public IViewFor ResolveView<T>(T viewModel, string contract = null) => (IViewFor)Locate(viewModel).Create();
}
