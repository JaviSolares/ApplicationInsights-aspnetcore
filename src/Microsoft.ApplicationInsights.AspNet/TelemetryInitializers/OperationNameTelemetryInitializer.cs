﻿namespace Microsoft.ApplicationInsights.AspNet.TelemetryInitializers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Linq;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.AspNet.Hosting;
    using Microsoft.AspNet.Http;
    using Microsoft.AspNet.Mvc;
    using Microsoft.AspNet.Mvc.Abstractions;
    using Microsoft.AspNet.Mvc.Routing;
    using Microsoft.AspNet.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DiagnosticAdapter;
    
    public class OperationNameTelemetryInitializer : TelemetryInitializerBase
    {
        public const string BeforeActionNotificationName = "Microsoft.AspNet.Mvc.BeforeAction";

        public OperationNameTelemetryInitializer(IHttpContextAccessor httpContextAccessor, DiagnosticListener telemetryListener) 
            : base(httpContextAccessor)
        {
            if (telemetryListener == null)
            {
                throw new ArgumentNullException("telemetryListener");
            }

            if (telemetryListener != null)
            {
                telemetryListener.SubscribeWithAdapter(this);
            }
        }

        public OperationNameTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
            : this(httpContextAccessor, null)
        {
        }

        protected override void OnInitializeTelemetry(HttpContext platformContext, RequestTelemetry requestTelemetry, ITelemetry telemetry)
        {
            if (string.IsNullOrEmpty(telemetry.Context.Operation.Name))
            {
                if (!string.IsNullOrEmpty(requestTelemetry.Name))
                {
                    telemetry.Context.Operation.Name = requestTelemetry.Name;
                }
                else
                {
                    // We didn't get BeforeAction notification
                    string name = platformContext.Request.Method + " " + platformContext.Request.Path.Value;
                    requestTelemetry.Name = name;
                    telemetry.Context.Operation.Name = name;
                }
            }
        }

        [DiagnosticName(BeforeActionNotificationName)]
        public void OnBeforeAction(ActionDescriptor actionDescriptor, HttpContext httpContext, RouteData routeData)
        {
            string name = this.GetNameFromRouteContext(routeData);
            var telemetry = httpContext.RequestServices.GetService<RequestTelemetry>();

            if (!string.IsNullOrEmpty(name) && telemetry != null && telemetry is RequestTelemetry)
            {
                name = httpContext.Request.Method + " " + name;
                ((RequestTelemetry)telemetry).Name = name;
            }
        }

        private string GetNameFromRouteContext(RouteData routeData)
        {
            string name = null;

            if (routeData.Values.Count > 0)
            {
                var routeValues = routeData.Values;

                object controller;
                routeValues.TryGetValue("controller", out controller);
                string controllerString = (controller == null) ? string.Empty : controller.ToString();

                if (!string.IsNullOrEmpty(controllerString))
                {
                    name = controllerString;

                    object action;
                    routeValues.TryGetValue("action", out action);
                    string actionString = (action == null) ? string.Empty : action.ToString();

                    if (!string.IsNullOrEmpty(actionString))
                    {
                        name += "/" + actionString;
                    }

                    if (routeValues.Keys.Count > 2)
                    {
                        // Add parameters
                        var sortedKeys = routeValues.Keys
                            .Where(key =>
                                !string.Equals(key, "controller", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(key, "action", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(key, AttributeRouting.RouteGroupKey, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        if (sortedKeys.Length > 0)
                        {
                            string arguments = string.Join(@"/", sortedKeys);
                            name += " [" + arguments + "]";
                        }
                    }
                }
            }

            return name;
        }
    }
}