﻿using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.ExpressApp.Model;
using JetBrains.Annotations;

namespace Xpand.Extensions.XAF.ModelExtensions{
    [PublicAPI]
    public partial class ModelExtensions{
        public static IEnumerable<IModelMemberViewItem> MemberViewItems(this IModelView modelObjectView, Type propertyEditorType=null)
            => !(modelObjectView is IModelObjectView) ? Enumerable.Empty<IModelMemberViewItem>()
                : (modelObjectView is IModelListView modelListView ? modelListView.Columns : ((IModelDetailView) modelObjectView).Items.OfType<IModelMemberViewItem>())
                .Where(item => propertyEditorType == null || propertyEditorType.IsAssignableFrom(item.PropertyEditorType));
    }
}