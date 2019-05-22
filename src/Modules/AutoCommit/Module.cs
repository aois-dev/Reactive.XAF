﻿using System;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.SystemModule;
using Xpand.Source.Extensions.XAF;
using Xpand.XAF.Modules.Reactive;
using Xpand.XAF.Modules.Reactive.Extensions;

namespace Xpand.XAF.Modules.AutoCommit{
    public sealed class AutoCommitModule : XafModule{
        public const string CategoryName = "Xpand.XAF.Modules.AutoCommit";

        public AutoCommitModule(){
            RequiredModuleTypes.Add(typeof(SystemModule));
            RequiredModuleTypes.Add(typeof(ReactiveModule));   
        }

        public override void Setup(ApplicationModulesManager moduleManager){
            base.Setup(moduleManager);
            Application.Connect()
                .TakeUntil(this.WhenDisposed())
                .Subscribe(unit => { }, () => { });
        }

        public override void ExtendModelInterfaces(ModelInterfaceExtenders extenders){
            base.ExtendModelInterfaces(extenders);
            extenders.Add<IModelClass, IModelClassAutoCommit>();
            extenders.Add<IModelObjectView, IModelObjectViewAutoCommit>();
        }

    }
}