using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Truss.AspNetCore
{
    /// <summary>
    /// Copies route values onto the command bound from the request body, so a
    /// PUT or PATCH command can carry the resource id in the URL and omit it
    /// from the JSON. The URL always wins: a body that contradicts the route
    /// cannot redirect the command to another resource.
    /// </summary>
    internal static class RouteValueMerger
    {
        private static readonly ConcurrentDictionary<(Type Command, string Name), PropertyInfo?> Properties = new();

        public static TCommand Merge<TCommand>(TCommand command, HttpContext httpContext)
        {
            foreach (var (name, value) in httpContext.Request.RouteValues)
            {
                if (value is not string text)
                    continue;

                var property = Properties.GetOrAdd((typeof(TCommand), name), static key =>
                    key.Command.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));

                if (property is null || property.SetMethod is null)
                    continue;

                property.SetValue(command, Convert(property, name, text));
            }

            return command;
        }

        private static object Convert(PropertyInfo property, string name, string text)
        {
            if (property.PropertyType == typeof(string))
                return text;

            try
            {
                return TypeDescriptor.GetConverter(property.PropertyType).ConvertFromInvariantString(text)!;
            }
            catch (Exception)
            {
                throw new BadHttpRequestException($"The route value '{name}' is not a valid {property.PropertyType.Name}.");
            }
        }
    }
}
