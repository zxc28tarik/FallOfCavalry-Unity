using System;
using System.Collections.Generic;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed class VisualSoldierPool:MonoBehaviour
    {
        [SerializeField]private VisualSoldier3D viewPrefab=null!;[SerializeField]private int capacity=512;private readonly Stack<VisualSoldier3D> available=new Stack<VisualSoldier3D>();private readonly HashSet<VisualSoldier3D> leased=new HashSet<VisualSoldier3D>();
        public int AvailableCount=>available.Count;public int LeasedCount=>leased.Count;public void Configure(VisualSoldier3D prefab,int maximum){if(prefab==null||maximum<1)throw new ArgumentException("Pool configuration is invalid.");viewPrefab=prefab;capacity=maximum;}
        public VisualSoldier3D Rent(){VisualSoldier3D value;if(available.Count>0)value=available.Pop();else{if(leased.Count>=capacity)throw new InvalidOperationException("Visual Soldier pool capacity exceeded.");value=Instantiate(viewPrefab,transform,false);}value.gameObject.SetActive(true);if(!leased.Add(value))throw new InvalidOperationException("Visual view was already leased.");return value;}
        public void Return(VisualSoldier3D value){if(value==null||!leased.Remove(value))throw new InvalidOperationException("Visual view is not leased by this pool.");value.ClearVisual();value.gameObject.SetActive(false);available.Push(value);}
    }
}
