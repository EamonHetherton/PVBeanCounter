using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;

namespace Mischel.Synchronization
{
    [Flags]
    public enum WaitableTimerRights
    {
        ChangePermissions = Win32WaitableTimer.WRITE_DAC,
        Delete = Win32WaitableTimer.DELETE,
        FullControl = Win32WaitableTimer.TIMER_ALL_ACCESS,
        Modify = Win32WaitableTimer.TIMER_MODIFY_STATE,
        ReadPermissions = Win32WaitableTimer.READ_CONTROL,
        Query = Win32WaitableTimer.TIMER_QUERY_STATE,
        Synchronize = Win32WaitableTimer.SYNCHRONIZE,
        TakeOwnership = Win32WaitableTimer.WRITE_OWNER
    }

    public sealed class WaitableTimerAccessRule : AccessRule
    {
        public WaitableTimerAccessRule(
            IdentityReference identity,
            WaitableTimerRights timerRights,
            AccessControlType type)
            : this(identity, (int)timerRights, false, InheritanceFlags.None, PropagationFlags.None, type)
        {
        }

        public WaitableTimerAccessRule(
            string identity,
            WaitableTimerRights timerRights,
            AccessControlType type)
            : this(new NTAccount(identity), (int)timerRights, false, InheritanceFlags.None, PropagationFlags.None, type)
        {
        }

        internal WaitableTimerAccessRule(
            IdentityReference identity,
            int accessMask,
            bool isInherited,
            InheritanceFlags inheritanceFlags,
            PropagationFlags propagationFlags,
            AccessControlType type)
            : base(identity, accessMask, isInherited, inheritanceFlags, propagationFlags, type)
        {
        }

        WaitableTimerRights WaitableTimerRights
        {
            get { return (WaitableTimerRights)AccessMask; }
        }
    }

    public sealed class WaitableTimerAuditRule : AuditRule
    {
        public WaitableTimerAuditRule(
            IdentityReference identity,
            WaitableTimerRights timerRights,
            AuditFlags flags)
            : this(identity, (int)timerRights, false, InheritanceFlags.None, PropagationFlags.None, flags)
        {
        }

        internal WaitableTimerAuditRule(
            IdentityReference identity,
            Int32 accessMask,
            bool isInherited,
            InheritanceFlags inheritanceFlags,
            PropagationFlags propagationFlags,
            AuditFlags flags)
            : base(identity, accessMask, isInherited, inheritanceFlags, propagationFlags, flags)
        {
        }

        public WaitableTimerRights WaitableTimerRights
        {
            get{ return (WaitableTimerRights)AccessMask; }
        }
    }

    
    public sealed class WaitableTimerSecurity : NativeObjectSecurity
    {

        public WaitableTimerSecurity()
            : base(true, ResourceType.KernelObject)
        {
        }

        internal WaitableTimerSecurity(
            SafeWaitHandle handle,
            AccessControlSections includeSections)
            : base(true, ResourceType.KernelObject, handle, includeSections, _HandleErrorCode, null)
        {
        }

        internal WaitableTimerSecurity(
            string name,
            AccessControlSections includeSections)
            : base(true, ResourceType.KernelObject, name, includeSections, _HandleErrorCode, null)
        {
        }

        private static Exception _HandleErrorCode(
            int errorcode,
            string name,
            SafeHandle handle,
            object context)
        {
            if (errorcode == Win32WaitableTimer.ERROR_FILE_NOT_FOUND ||
                errorcode == Win32WaitableTimer.ERROR_INVALID_HANDLE ||
                errorcode == Win32WaitableTimer.ERROR_INVALID_NAME)
            {
                if (name == null)
                {
                    return new WaitHandleCannotBeOpenedException();
                }
                return new WaitHandleCannotBeOpenedException("Invalid handle");
            }
            return null;
        }

        public override Type AccessRightType
        {
            get { return typeof(WaitableTimerRights); }
        }

        public override AccessRule AccessRuleFactory(
            IdentityReference identity, 
            int accessMask, 
            bool isInherited, 
            InheritanceFlags inheritanceFlags, 
            PropagationFlags propagationFlags, 
            AccessControlType type)
        {
            return new WaitableTimerAccessRule(identity, accessMask,
                isInherited, inheritanceFlags, propagationFlags, type);
        }

        public override Type AccessRuleType
        {
            get { return typeof(WaitableTimerAccessRule); }
        }

        public override AuditRule AuditRuleFactory(
            IdentityReference identity,
            int accessMask,
            bool isInherited,
            InheritanceFlags inheritanceFlags,
            PropagationFlags propagationFlags,
            AuditFlags flags)
        {
            return new WaitableTimerAuditRule(identity, accessMask,
                isInherited, inheritanceFlags, propagationFlags, flags);
        }

        public override Type AuditRuleType
        {
            get { return typeof(WaitableTimerAuditRule); }
        }

        public void AddAccessRule(WaitableTimerAccessRule rule)
        {
            base.AddAccessRule(rule);
        }

        public void AddAuditRule(WaitableTimerAuditRule rule)
        {
            base.AddAuditRule(rule);
        }

        public void RemoveAccessRule(WaitableTimerAccessRule rule)
        {
            base.RemoveAccessRule(rule);
        }

        public void RemoveAccessRuleAll(WaitableTimerAccessRule rule)
        {
            base.RemoveAccessRuleAll(rule);
        }

        public void RemoveAccessRuleSpecific(WaitableTimerAccessRule rule)
        {
            base.RemoveAccessRuleSpecific(rule);
        }

        public void RemoveAuditRule(WaitableTimerAuditRule rule)
        {
            base.RemoveAuditRule(rule);
        }

        public void RemoveAuditRuleAll(WaitableTimerAuditRule rule)
        {
            base.RemoveAuditRuleAll(rule);
        }

        public void RemoveAuditRuleSpecific(WaitableTimerAuditRule rule)
        {
            base.RemoveAuditRuleSpecific(rule);
        }

        public void ResetAccessRule(WaitableTimerAccessRule rule)
        {
            base.ResetAccessRule(rule);
        }

        public void SetAccessRule(WaitableTimerAccessRule rule)
        {
            base.SetAccessRule(rule);
        }

        public void SetAuditRule(WaitableTimerAuditRule rule)
        {
            base.SetAuditRule(rule);
        }

        internal AccessControlSections GetAccessControlSectionsFromChanges()
        {
            AccessControlSections sections = AccessControlSections.None;
            if (AccessRulesModified)
            {
                sections |= AccessControlSections.Access;
            }
            if (AuditRulesModified)
            {
                sections |= AccessControlSections.Audit;
            }
            if (OwnerModified)
            {
                sections |= AccessControlSections.Owner;
            }
            if (GroupModified)
            {
                sections |= AccessControlSections.Group;
            }
            return sections;
        }

        internal void Persist(SafeWaitHandle SafeWaitHandle)
        {
            WriteLock();
            try
            {
                AccessControlSections sections = GetAccessControlSectionsFromChanges();
                if (sections == AccessControlSections.None)
                {
                    return;
                }
                base.Persist(SafeWaitHandle, sections);
                AccessRulesModified = false;
                AuditRulesModified = false;
                OwnerModified = false;
                GroupModified = false;
            }
            finally
            {
                WriteUnlock();
            }
        }
    }
}
