using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Service.LocalizationService;
using Service.UserService;
using Service.RouteService;
using Service.SkinService;
using UnityEngine.SceneManagement;

namespace App
{
    public class AppPresenter : IStartable
    {
        [Inject] private IUserService UserService { get; set; }
        [Inject] private IRouteService RouteService { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private ISkinService SkinService { get; set; }
        [Inject] private AppLifetimeScope AppScope { get; set; }

        public async void Start()
        {
            Application.targetFrameRate = 60;

            UserService.Initialize();
            await LocalizationService.InitializeAsync();

            // Boundary defense: a missing/broken skin (e.g. classic id renamed, offline remote
            // load) must NOT freeze app startup. Surface the error and continue routing — Settings
            // can be reopened to pick a working skin once the user is in.
            try
            {
                await SkinService.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            RouteService.Initialize(async path =>
            {
                LifetimeScope.EnqueueParent(AppScope);
                await SceneManager.LoadSceneAsync(path).ToUniTask();
            });

            // Stats / Achievement / GPGS init + Firebase Auth는 모두 LoginPresenter의
            // consent 게이트 뒤에서 실행. App은 Localization만 끝내고 Login으로 라우팅.
            await RouteService.NavigateAsync("Login", useBlocker: false);
        }
    }
}
