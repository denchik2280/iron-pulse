using System.Collections.Generic;
using UnityEngine;

namespace IronPulse
{
 public sealed partial class IPPlayer
 {
  enum SpecialAction { None, Punch, Ultimate }
  SpecialAction specialAction;
  float specialTime,specialRecovery,footstepTime;
  int ultimateStrike;
  bool punchContact;
  readonly HashSet<IPEnemy> ultimateVictims=new HashSet<IPEnemy>();
  readonly RaycastHit[] specialVisibilityHits=new RaycastHit[32];
  public float UltimateCharge {get;private set;}
  public bool UltimateReady=>UltimateCharge>=99.99f;
  public bool IsPunching=>specialAction==SpecialAction.Punch;
  public bool IsUltimateActive=>specialAction==SpecialAction.Ultimate;
  public bool IsSpecialAttacking=>specialAction!=SpecialAction.None;
  public int PunchesThrown {get;private set;}
  public int MeleeHits {get;private set;}
  public int UltimateActivations {get;private set;}
  public int UltimateKills {get;private set;}
  public float SpecialProgress=>IsSpecialAttacking?Mathf.Clamp01(specialTime/(IsUltimateActive?1.65f:.56f)):0f;

  public void AddUltimateCharge(float amount)
  {
   if(amount<=0f||IsDead||IsUltimateActive)return;
   bool ready=UltimateReady;UltimateCharge=Mathf.Clamp(UltimateCharge+amount,0f,100f);
   if(!ready&&UltimateReady){IPGame.Audio("ultimate_ready",transform.position,.65f);IPGame.Label("[ F ]  KATANA RAMPAGE READY",new Color(1f,.36f,.08f));}
  }
  public bool TrySpecialAttack()
  {
   if(!initialized||IsDead||IsExecuting||IsSpecialAttacking||specialRecovery>0f||IPGame.IsPaused)return false;
   specialTime=0f;punchContact=false;ultimateStrike=0;
   if(UltimateReady)
   {
    specialAction=SpecialAction.Ultimate;UltimateCharge=0f;UltimateActivations++;ultimateVictims.Clear();
    Weapon.StartUltimate();IPGame.Label("KATANA // RAMPAGE",new Color(1f,.36f,.08f));
   }
   else {specialAction=SpecialAction.Punch;PunchesThrown++;Weapon.StartPunch();}
   return true;
  }
  void UpdateSpecialAttack(float dt)
  {
   specialRecovery=Mathf.Max(0f,specialRecovery-dt);
   if(!IsSpecialAttacking)return;
   specialTime+=dt;float t=SpecialProgress;
   if(IsPunching)
   {
    IPEnemy aimed=FindPunchTarget();
    if(!punchContact&&aimed)Weapon.SetPunchVictim(PunchSurfacePoint(aimed));
    Weapon.SetPunchProgress(t);
    if(!punchContact&&t>=.34f)
    {
     punchContact=true;IPEnemy target=aimed;
     AddShotImpulse(.65f,-.55f);
     if(target)
     {
      float before=target.Health;target.ReceiveMelee(34f,target.ExecutionChestPoint,ViewCamera.transform.forward);
      if(target.Health<before||target.IsDead){MeleeHits++;HitConfirm=1f;IPGame.Hit();AddUltimateCharge(6f);}
      IPGame.Audio("melee_hit",target.ExecutionChestPoint,.85f);
     }
    }
   }
   else
   {
    Weapon.SetUltimateProgress(t);
    // The same three normalized contact times drive the blade and the crowd cuts.
    while(ultimateStrike<3&&t>=.25f+ultimateStrike*.21f){UltimateSweep(ultimateStrike);ultimateStrike++;}
   }
   if(t>=1f)
   {
    if(IsPunching)Weapon.EndPunch();else Weapon.EndUltimate();
    specialAction=SpecialAction.None;specialTime=0f;specialRecovery=.12f;
   }
  }
  IPEnemy FindPunchTarget()
  {
   IPEnemy target=null;float best=2.15f;
   foreach(var enemy in IPEnemy.Active)
   {
    if(!enemy||enemy.IsDead)continue;Vector3 d=enemy.ExecutionChestPoint-ViewCamera.transform.position;
    if(d.magnitude>=best||Vector3.Dot(ViewCamera.transform.forward,d.normalized)<.62f||!SpecialVisible(enemy))continue;
    target=enemy;best=d.magnitude;
   }
   return target;
  }
  Vector3 PunchSurfacePoint(IPEnemy target)
  {
   Vector3 origin=ViewCamera.transform.position,delta=target.ExecutionChestPoint-origin;
   var collider=target.GetComponent<Collider>();
   if(collider&&collider.Raycast(new Ray(origin,delta.normalized),out var hit,delta.magnitude+.8f))return hit.point;
   return target.ExecutionChestPoint-delta.normalized*.25f;
  }
  bool SpecialVisible(IPEnemy enemy)
  {
   Vector3 origin=ViewCamera.transform.position,delta=enemy.ExecutionChestPoint-origin;
   int count=Physics.RaycastNonAlloc(origin,delta.normalized,specialVisibilityHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
   for(int i=0;i<count;i++)
   {
    var collider=specialVisibilityHits[i].collider;
    if(!collider||collider.GetComponentInParent<IPPlayer>()||collider.GetComponentInParent<IPEnemy>())continue;
    return false;
   }
   return true;
  }
  void UltimateSweep(int index)
  {
   Vector3 origin=ViewCamera.transform.position,right=ViewCamera.transform.right,forward=ViewCamera.transform.forward;
   Vector3 cut=(right*(index==1?-1f:1f)+Vector3.down*(index==2?.8f:.22f)).normalized;
   int hit=0;
   // Killing removes a victim from Active, so damage always iterates a stable snapshot.
   foreach(var enemy in IPEnemy.Active.ToArray())
   {
    if(!enemy||enemy.IsDead||ultimateVictims.Contains(enemy))continue;
    Vector3 delta=enemy.ExecutionChestPoint-origin;float lateral=Vector3.Dot(delta,right);
    if(delta.magnitude>7.25f||Vector3.Dot(delta.normalized,forward)<.18f||!SpecialVisible(enemy))continue;
    if(index==0&&lateral>.65f||index==1&&lateral<-.65f)continue;
    enemy.ReceiveUltimate(enemy.ExecutionChestPoint,cut);
    if(enemy.IsDead){ultimateVictims.Add(enemy);hit++;UltimateKills++;}
   }
   AddShotImpulse(hit>0?2.1f:1.1f,index==1?-1.1f:1.1f);
   IPGame.Audio("ultimate_swing",origin,.85f);
   if(hit>0){HitConfirm=1f;IPGame.Hit(true);IPGame.Audio("metalTear",origin,.85f);}
  }
  void ApplySpecialMotion(float dt)
  {
   if(IsPunching&&SpecialProgress>.08f&&SpecialProgress<.34f&&controller.enabled)
   {
    IPEnemy target=null;float closest=2.45f;
    foreach(var enemy in IPEnemy.Active)
    {
     if(!enemy||enemy.IsDead)continue;Vector3 d=enemy.ExecutionChestPoint-ViewCamera.transform.position;
     if(d.magnitude<closest&&Vector3.Dot(d.normalized,ViewCamera.transform.forward)>.7f&&SpecialVisible(enemy)){target=enemy;closest=d.magnitude;}
    }
    if(target&&closest>1.15f)
    {
     Vector3 d=target.transform.position-transform.position;d.y=0f;
     float drive=Mathf.Sin(Mathf.InverseLerp(.08f,.34f,SpecialProgress)*Mathf.PI);
     controller.Move(d.normalized*Mathf.Min(closest-1.15f,3f*drive*dt));
    }
    return;
   }
   if(!IsUltimateActive||SpecialProgress<.10f||SpecialProgress>.72f||!controller.enabled)return;
   IPEnemy focus=null;float best=9f;
   foreach(var enemy in IPEnemy.Active)
   {
    if(!enemy||enemy.IsDead)continue;Vector3 d=enemy.transform.position-transform.position;d.y=0f;
    float distance=d.magnitude;if(distance>=best||Vector3.Dot(d.normalized,transform.forward)<.45f||!SpecialVisible(enemy))continue;
    focus=enemy;best=distance;
   }
   if(focus&&best>1.55f)
   {
    Vector3 direction=focus.transform.position-transform.position;direction.y=0f;
    controller.Move(direction.normalized*Mathf.Min(best-1.55f,5.2f*dt));
   }
  }
  void ApplySpecialCamera()
  {
   if(!IsSpecialAttacking)return;float t=SpecialProgress;
   float drive=Mathf.Sin(t*Mathf.PI);drive*=drive;
   float contact=IsPunching?ContactPulse(t,.34f,.12f):ContactPulse(t,.25f,.08f)+ContactPulse(t,.46f,.08f)+ContactPulse(t,.67f,.08f);
   ViewCamera.transform.localPosition+=new Vector3(0f,-drive*(IsPunching?.014f:.028f),drive*(IsPunching?.026f:.055f));
   ViewCamera.transform.localRotation*=Quaternion.Euler(-contact*1.7f,0f,(IsPunching?-1f:Mathf.Sin(t*Mathf.PI*4f))*drive*2.4f);
  }
  float SpecialFovOffset()
  {
   if(!IsSpecialAttacking)return 0f;
   float drive=Mathf.Sin(SpecialProgress*Mathf.PI);return drive*drive*(IsUltimateActive?5f:1.3f);
  }
  void UpdateFootsteps(float dt,float movement)
  {
   footstepTime-=dt;
   if(grounded&&movement>.2f&&dashRemaining<=0f&&!IsSpecialAttacking&&footstepTime<=0f)
   {footstepTime=Mathf.Lerp(.40f,.28f,movement);IPGame.Audio("footstep",transform.position,.24f);}
  }
  void CancelSpecialAttack()
  {
   if(Weapon!=null){if(IsPunching)Weapon.EndPunch();if(IsUltimateActive)Weapon.EndUltimate();}
   specialAction=SpecialAction.None;specialTime=0f;ultimateVictims.Clear();
  }
  void ResetSpecialCombat()
  {
   CancelSpecialAttack();specialRecovery=footstepTime=0f;UltimateCharge=0f;
   PunchesThrown=MeleeHits=UltimateActivations=UltimateKills=0;
  }
 }
}
