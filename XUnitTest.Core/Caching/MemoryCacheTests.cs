﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Caching
{
    public class MemoryCacheTests
    {
        public MemoryCache Cache { get; set; }

        public MemoryCacheTests()
        {
            Cache = new MemoryCache();
        }

        [Fact(DisplayName = "基础测试")]
        public void Test1()
        {
            var ic = Cache;
            var key = "Name";
            var key2 = "Company";

            ic.Set(key, "大石头");
            ic.Set(key2, "新生命");
            Assert.Equal("大石头", ic.Get<String>(key));
            Assert.Equal("新生命", ic.Get<String>(key2));

            var count = ic.Count;
            Assert.True(count >= 2);

            // Keys
            var keys = ic.Keys;
            Assert.True(keys.Contains(key));

            // 过期时间
            ic.SetExpire(key, TimeSpan.FromSeconds(1));
            var ts = ic.GetExpire(key);
            Assert.True(ts.TotalSeconds > 0 && ts.TotalSeconds < 2, "过期时间");

            var rs = ic.Remove(key2);
            Assert.Equal(1, rs);

            Assert.False(ic.ContainsKey(key2));

            ic.Clear();
            Assert.True(ic.Count == 0);
        }

        [Fact(DisplayName = "集合测试")]
        public void DictionaryTest()
        {
            var ic = Cache;

            var dic = new Dictionary<String, String>
            {
                ["111"] = "123",
                ["222"] = "abc",
                ["大石头"] = "学无先后达者为师"
            };

            ic.SetAll(dic);
            var dic2 = ic.GetAll<String>(dic.Keys);

            Assert.Equal(dic.Count, dic2.Count);
            foreach (var item in dic)
            {
                Assert.Equal(item.Value, dic2[item.Key]);
            }
        }

        [Fact(DisplayName = "高级添加")]
        public void AddReplace()
        {
            var ic = Cache;
            var key = "Name";

            ic.Set(key, Environment.UserName);
            var rs = ic.Add(key, Environment.MachineName);
            Assert.False(rs);

            var name = ic.Get<String>(key);
            Assert.Equal(Environment.UserName, name);
            Assert.NotEqual(Environment.MachineName, name);

            var old = ic.Replace(key, Environment.MachineName);
            Assert.Equal(Environment.UserName, old);

            name = ic.Get<String>(key);
            Assert.Equal(Environment.MachineName, name);
            Assert.NotEqual(Environment.UserName, name);
        }

        [Fact(DisplayName = "累加累减")]
        public void IncDec()
        {
            var ic = Cache;
            var key = "CostInt";
            var key2 = "CostDouble";

            ic.Set(key, 123);
            ic.Increment(key, 22);
            Assert.Equal(123 + 22, ic.Get<Int32>(key));

            ic.Set(key2, 456d);
            ic.Increment(key2, 22d);
            Assert.Equal(456d + 22d, ic.Get<Double>(key2));

            ic.Set("cc", 3.14);
            ic.Increment("cc", 0.3);
            Assert.Equal(3.14 + 0.3, ic.Get<Double>("cc"));
        }

        [Fact(DisplayName = "复杂对象")]
        public void TestObject()
        {
            var obj = new User
            {
                Name = "大石头",
                Company = "NewLife",
                Age = 24,
                Roles = new[] { "管理员", "游客" },
                UpdateTime = DateTime.Now,
            };

            var ic = Cache;
            var key = "user";

            ic.Set(key, obj);
            var obj2 = ic.Get<User>(key);

            Assert.Equal(obj.ToJson(), obj2.ToJson());
        }

        class User
        {
            public String Name { get; set; }
            public String Company { get; set; }
            public Int32 Age { get; set; }
            public String[] Roles { get; set; }
            public DateTime UpdateTime { get; set; }
        }

        [Fact(DisplayName = "字节数组")]
        public void TestBuffer()
        {
            var ic = Cache;
            var key = "buf";

            var str = "学无先后达者为师";
            var buf = str.GetBytes();

            ic.Set(key, buf);
            var buf2 = ic.Get<Byte[]>(key);

            Assert.Equal(buf.ToHex(), buf2.ToHex());
        }

        [Fact(DisplayName = "数据包")]
        public void TestPacket()
        {
            var ic = Cache;
            var key = "buf";

            var str = "学无先后达者为师";
            var pk = new Packet(str.GetBytes());

            ic.Set(key, pk);
            var pk2 = ic.Get<Packet>(key);

            Assert.Equal(pk.ToHex(), pk2.ToHex());
        }

        [Fact(DisplayName = "正常锁")]
        public void TestLock1()
        {
            var ic = Cache;

            var ck = ic.AcquireLock("TestLock1", 3000);
            var k2 = ck as CacheLock;

            Assert.NotNull(k2);
            Assert.Equal("lock:TestLock1", k2.Key);

            // 实际上存在这个key
            Assert.True(ic.ContainsKey(k2.Key));

            // 取有效期
            var exp = ic.GetExpire(k2.Key);
            Assert.True(exp.TotalMilliseconds <= 3000);

            // 释放锁
            ck.Dispose();

            // 这个key已经不存在
            Assert.False(ic.ContainsKey(k2.Key));
        }

        [Fact(DisplayName = "抢锁失败")]
        public void TestLock2()
        {
            var ic = Cache;

            var ck1 = ic.AcquireLock("TestLock2", 3000);

            var sw = Stopwatch.StartNew();

            // 抢相同锁，不可能成功。超时时间必须小于3000，否则前面的锁过期后，这里还是可以抢到的
            Assert.Throws<InvalidOperationException>(() => ic.AcquireLock("TestLock2", 2000));

            // 耗时必须超过有效期
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds >= 2000 - 200);

            Thread.Sleep(3000 - 2000 + 1);

            // 那个锁其实已经不在了，缓存应该把它干掉
            Assert.False(ic.ContainsKey("lock:TestLock2"));
        }

        [Fact(DisplayName = "抢死锁")]
        public void TestLock3()
        {
            var ic = Cache;

            var ck = ic.AcquireLock("TestLock3", 3000);

            // 已经过了一点时间
            Thread.Sleep(2000);

            // 循环多次后，可以抢到
            var ck2 = ic.AcquireLock("TestLock3", 3000);
            Assert.NotNull(ck2);
        }
    }
}