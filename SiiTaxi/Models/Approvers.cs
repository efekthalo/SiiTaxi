//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SiiTaxi.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Approvers
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Approvers()
        {
            this.Taxi = new HashSet<Taxi>();
        }
    
        public int PeopleId { get; set; }
        public bool IsApprover { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Taxi> Taxi { get; set; }
        public virtual People People { get; set; }
    }
}