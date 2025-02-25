﻿using System;
using System.ComponentModel;
using DevExpress.ExpressApp.DC;
using Xpand.Extensions.XAF.ObjectExtensions;

namespace Xpand.Extensions.XAF.NonPersistentObjects {
    [DomainComponent]
    [XafDefaultProperty(nameof(Name))]
    public class ObjectType:NonPersistentBaseObject{
        private string _name;
        private Type _type;

        public ObjectType(Type type) {
            Type = type;
            Name = type?.Name.CompoundName();
        }

        [DevExpress.ExpressApp.Data.Key]
        public string Name {
            get => _name;
            set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        public Type Type {
            get => _type;
            set {
                if (value == _type) return;
                _type = value;
                OnPropertyChanged();
            }
        }
    }
}