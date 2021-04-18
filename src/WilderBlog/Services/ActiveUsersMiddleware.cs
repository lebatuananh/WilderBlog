﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WilderBlog.Services
{
  public class ActiveUsersMiddleware : IMiddleware
  {
    public const string COOKIENAME = ".Vanity.WilderBlog";
    const string PREFIX = "ActiveUser_";
    private const int TIMEOUTMINUTES = 5;
    private IMemoryCache _cache;
    private ILogger<ActiveUsersMiddleware> _logger;

    public ActiveUsersMiddleware(IMemoryCache cache, ILogger<ActiveUsersMiddleware> logger)
    {
      _cache = cache;
      _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
      try
      {
        if (!context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/livewriter"))
        {
          string cookie;
          if (context.Request.Cookies.ContainsKey(COOKIENAME))
          {
            cookie = context.Request.Cookies[COOKIENAME]!;
          }
          else
          {
            cookie = Guid.NewGuid().ToString();
          }

          var key = $"{PREFIX}{cookie}";
          var expiration = DateTime.UtcNow.AddMinutes(TIMEOUTMINUTES);
          _cache.Remove(key);
          _cache.Set<object>(key, expiration, expiration);
          context.Response.Cookies.Append(COOKIENAME, cookie, new CookieOptions() { Expires = DateTimeOffset.Now.AddMinutes(TIMEOUTMINUTES) });
        }
      }
      catch
      {
        _logger.LogError("Failed to store active user");
      }

      await next.Invoke(context);
    }

    public static long GetActiveUserCount(IMemoryCache cache)
    {
      var cacheType = cache.GetType();
      var fieldInfo = cacheType.GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
      if (fieldInfo is not null)
      {

        var dict = fieldInfo.GetValue(cache);

        if (dict is not null)
        {

          var keys = ((IDictionary)dict).Keys
            .Cast<object>()
            .Where(k => k is string && ((string)k).StartsWith(PREFIX))
            .Cast<string>()
            .ToList();

          return keys.Count(k =>
          {
            DateTime expiration;
            if (cache.TryGetValue<DateTime>(k, out expiration))
            {
              if (expiration > DateTime.UtcNow)
              {
                return true;
              }
            }
            return false;
          });

        }
      }

      return 0;

    }

  }
}
