﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace PngPreview
{
    public class PngImagesController : MonoBehaviour
    {
        private static readonly TimeSpan _1_SEC = new TimeSpan(0, 0, 1);
        private static readonly SavedJSONPath<string[]> CACHED_PATHS_LIST = SavedJSONPath<string[]>.Create("CACHED_PATHS_LIST");

        [SerializeField] private Transform listContainer;
        [SerializeField] private PngPreviewItem itemPrefab;
        [SerializeField] private Button addItem;
        [SerializeField] private Button refreshList;

        private Dictionary<string, PngPreviewItem> spawnedItems;

        public IObservable<long> Timer { get; private set; }

        private void OnValidate()
        {
            Assert.IsNotNull(itemPrefab, $"{nameof(itemPrefab)} is null");
            Assert.IsNotNull(addItem, $"{nameof(addItem)} is null");
            Assert.IsNotNull(refreshList, $"{nameof(refreshList)} is null");
        }

        private void Awake()
        {
            spawnedItems = new Dictionary<string, PngPreviewItem>();
            Timer = Observable.Interval(_1_SEC);

            Init();
        }

        private void OnEnable()
        {
            addItem.onClick.AsObservable().TakeUntilDisable(gameObject).Subscribe(i => UploadImageFromGallery());
            refreshList.onClick.AsObservable().TakeUntilDisable(gameObject).Subscribe(i => RefreshList());
        }

        private void Init()
        {
            var paths = SavingUtil.LoadJSON(CACHED_PATHS_LIST);
            if (paths == null) return;

            foreach (var path in paths)
            {
                LoadImageByPath(path);
            }
        }

        public void RemoveItem(string itemPath)
        {
            if (spawnedItems.TryGetValue(itemPath, out var item))
            {
                Debug.Log($"RemovedItem: {itemPath}");
                Destroy(item.gameObject);
                spawnedItems.Remove(itemPath);
                SavingUtil.SaveAsJSON(spawnedItems.Keys.ToArray(), CACHED_PATHS_LIST);
            }
        }

        private void UploadImageFromGallery()
        {
            NativeGallery.GetImageFromGallery((imagePath) =>
            {
                Debug.Log($"Image path: {imagePath}");
                if (imagePath != null)
                {
                    var data = LoadPNGImage(imagePath);

                    SpawnItem(data);
                }
            });
        }

        private void RefreshList()
        {
            var pathList = spawnedItems.Select(i => i.Key).ToArray();
            foreach (var path in pathList)
            {
                RemoveItem(path);
            }

            foreach (var path in pathList)
            {
                LoadImageByPath(path);
            }
        }

        private void LoadImageByPath(string imagePath)
        {
            NativeGallery.Permission permission = NativeGallery.RequestPermission(NativeGallery.PermissionType.Read);

            if (permission != NativeGallery.Permission.Granted)
            {
                Debug.LogError($"Failed to load file data due to permission is {imagePath}");
                return;
            }

            var data = LoadPNGImage(imagePath);

            SpawnItem(data);
        }

        private void SpawnItem(PngPreviewItem.ImageData data)
        {
            if (data == null)
            {
                Debug.LogError("Failed to spawn ImagePreview item due to data is null");
                return;
            }

            if (spawnedItems.TryGetValue(data.fullPath, out var alreadySpawnedItem))
            {
                alreadySpawnedItem.UpdateData(data);
                Debug.Log($"UpdatedItem: {data.fullPath}");
            }
            else
            {
                var listItem = Instantiate(itemPrefab, listContainer);
                listItem.Init(data, this);

                spawnedItems.Add(data.fullPath, listItem);
                Debug.Log($"SpawnedItem: {data.fullPath}");
                SavingUtil.SaveAsJSON(spawnedItems.Keys.ToArray(), CACHED_PATHS_LIST);
            }
        }

        private PngPreviewItem.ImageData LoadPNGImage(string imagePath)
        {
            // TODO UNCOMMENT TO LOAD ONLY .PNG
            // var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            // if (extension != ".png")
            // {
            //     Debug.LogError($"Failed to load file due to it's extension is not a .png");
            //     return null;
            // }

            if (File.Exists(imagePath) == false)
            {
                Debug.LogError($"Failed to load file due to it doesn't exist. Path: {imagePath}");
                return null;
            }

            var fileData = File.ReadAllBytes(imagePath);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);

            var createdDate = File.GetCreationTime(imagePath);
            return new PngPreviewItem.ImageData(imagePath, texture, createdDate.ToUniversalTime());
        }
    }
}