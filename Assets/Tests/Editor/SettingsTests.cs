using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CineScape.Tests
{
    public class SettingsTests
    {
        string directory;
        [SetUp] public void Setup(){ directory=Path.Combine(Path.GetTempPath(),"CineScape-tests-"+Guid.NewGuid());Directory.CreateDirectory(directory); }
        [TearDown] public void Cleanup(){ Directory.Delete(directory,true); }
        [Test] public void CorruptSettingsFallBack()
        {
            var path=Path.Combine(directory,"settings.json");File.WriteAllText(path,"not json");
            var manager=new SettingsManager(path);Assert.That(manager.Data.lastDistance,Is.EqualTo(.5f));Assert.That(manager.LastError,Is.Not.Null);
        }
        [Test] public void SettingsRoundTrip()
        {
            var path=Path.Combine(directory,"settings.json");var manager=new SettingsManager(path);
            manager.Data.lastDistance=.73f;manager.Data.volume=.24f;manager.Data.keystone=.62f;manager.Save();manager.Save();
            var loaded=new SettingsManager(path);Assert.That(loaded.Data.lastDistance,Is.EqualTo(.73f).Within(.001));Assert.That(loaded.Data.volume,Is.EqualTo(.24f).Within(.001));
            Assert.That(loaded.Data.keystone,Is.EqualTo(.62f).Within(.001));
        }
        [Test] public void InvalidValuesAreNormalized()
        {
            var data=new AppSettings{lastDistance=float.NaN,volume=4,quality=-2};data.Normalize();
            Assert.That(data.lastDistance,Is.EqualTo(.5f));Assert.That(data.volume,Is.EqualTo(1));Assert.That(data.quality,Is.EqualTo(0));
        }
        [Test] public void MissingFieldsPreserveDefaults()
        {
            var path=Path.Combine(directory,"settings.json");File.WriteAllText(path,"{}");
            var manager=new SettingsManager(path);
            Assert.That(manager.Data.defaultVolume,Is.EqualTo(.8f));Assert.That(manager.Data.defaultDistance,Is.EqualTo(.5f));
            Assert.That(manager.Data.rememberDistance,Is.True);
            Assert.That(manager.Data.keystone,Is.EqualTo(1));
        }
        [Test] public void CameraStaysOnSafeAxis()
        {
            var o=new GameObject("camera test",typeof(UnityEngine.Camera),typeof(CameraDistanceController));
            try
            {
                var d=o.GetComponent<CameraDistanceController>();
                foreach(var value in new[]{-10f,0f,.5f,1f,10f})
                {
                    d.Initialize(new Vector3(0,5.5f,1.05f),30,16,value);
                    d.SetKeystone(0);
                    Assert.That(o.transform.position.x,Is.EqualTo(0));Assert.That(o.transform.position.z,Is.InRange(5.53f-.0001f,29.05f+.0001f));
                    Assert.That(o.transform.position.y,Is.EqualTo(2.3f+CinemaSceneController.SeatingHeight(o.transform.position.z)).Within(.0001f));
                    Assert.That(Vector3.Dot(o.transform.forward,(new Vector3(0,5.5f,1.05f)-o.transform.position).normalized),Is.GreaterThan(.999f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(o); }
        }
        [Test]
        public void KeystoneKeepsScreenRectangularAndCentered()
        {
            var o=new GameObject("projection test",typeof(UnityEngine.Camera),typeof(CameraDistanceController));
            try
            {
                var view=o.GetComponent<UnityEngine.Camera>();var d=o.GetComponent<CameraDistanceController>();
                foreach(var aspect in new[]{4f/3,16f/9,21f/9})foreach(var distance in new[]{0f,.5f,1f})
                {
                    view.aspect=aspect;d.Initialize(new Vector3(0,5.5f,1.05f),30,16,distance);d.SetKeystone(1);
                    var bl=view.WorldToViewportPoint(new Vector3(-8,1,1.05f));var br=view.WorldToViewportPoint(new Vector3(8,1,1.05f));
                    var tl=view.WorldToViewportPoint(new Vector3(-8,10,1.05f));var tr=view.WorldToViewportPoint(new Vector3(8,10,1.05f));
                    Assert.That(tl.x,Is.EqualTo(bl.x).Within(.0001));Assert.That(tr.x,Is.EqualTo(br.x).Within(.0001));
                    Assert.That(tl.y,Is.EqualTo(tr.y).Within(.0001));Assert.That(bl.y,Is.EqualTo(br.y).Within(.0001));
                    var center=view.WorldToViewportPoint(new Vector3(0,5.5f,1.05f));
                    Assert.That(center.x,Is.EqualTo(.5f).Within(.0001));Assert.That(center.y,Is.EqualTo(.5f).Within(.0001));
                    Assert.That(Mathf.Abs(tr.x-tl.x)*aspect/Mathf.Abs(tl.y-bl.y),Is.EqualTo(16f/9).Within(.001));
                    d.SetKeystone(0);
                    var originalTop=view.WorldToViewportPoint(new Vector3(8,10,1.05f))-view.WorldToViewportPoint(new Vector3(-8,10,1.05f));
                    var originalBottom=view.WorldToViewportPoint(new Vector3(8,1,1.05f))-view.WorldToViewportPoint(new Vector3(-8,1,1.05f));
                    Assert.That(Mathf.Abs(originalBottom.x),Is.GreaterThan(Mathf.Abs(originalTop.x)));
                }
                d.SetKeystone(float.NaN);Assert.That(d.Keystone,Is.EqualTo(1));
                d.SetKeystone(-1);Assert.That(d.Keystone,Is.EqualTo(0));d.SetKeystone(2);Assert.That(d.Keystone,Is.EqualTo(1));
            }
            finally{UnityEngine.Object.DestroyImmediate(o);}
        }
        [TestCase(59,"00:59")]
        [TestCase(3661,"01:01:01")]
        public void TimeFormatting(double seconds,string expected){Assert.That(PlayerUIController.Format(seconds),Is.EqualTo(expected));}
    }
}
