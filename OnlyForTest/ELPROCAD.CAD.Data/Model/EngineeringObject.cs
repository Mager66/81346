using DevExpress.Xpo;
using System;

namespace ELPROCAD.CAD.Data.Model
{
	[Persistent("Objects")]
	public class EngineeringObject : PersistentEntity
	{
		/* Structure reference according to IEC81346
		 * 
		 *  Power function (KKS)
		 *  Prefix: '==='
		 *  Description: The objecs included in the power supply system.
		 *				 Can also be used for designations on IO-signals
		 * 
		 * Control function
		 * Prefix: '=='
		 * Description:	The objects included in a control system.
		 *			    Can also be used for designations on IO-signals
		 *
		 * Topograpich building structure
		 * Prefix: '++'
		 * Description: Physical building structure
		 * 
		 * Function / Processfunction
		 * Prefix: '='
		 * Description: Functions in a plant. What is performed.
		 *			    Can also be used for designations on IO-signals
		 * 
		 * Location (placement), Enclosure structure
		 * Prefix: '+'
		 * Description: Where is the harware inside the plant located
		 * 
		 * Product / Productstructure
		 * Prefix: '-'
		 * Which products (components) doing the work/task
		 * 
		 * Controladdress
		 * Prefix: '#'
		 * Description: Used to give designate the control systems logical topographi
		 * 
		 * Free / Cables
		 * Prefix: none
		 * Description: Used for cable- and wiring numbering. Can also be used for tasks on IO-signals
		 */
	
		private Project _project = null;
		private Sheet _sheet = null;
		private Location? _location;
		private Function _function;

		public EngineeringObject(Session session) : base(session) { }

		/// <summary>
		/// According to IEC81346 = Location(tree), this should just be seen as an example though we are out for supporting "any" standard so the meaning of this property can change
		/// Corresponds to tag DES1 in CAD
		/// IEC81346 prefix:: '+'
		/// </summary>
		public string Designation1 {  get; set; }

		/// <summary>
		/// According to IEC81346 = Function (tree), this should just be seen as an example though we are out for supporting "any" standard so the meaning of this property can change
		/// Corresponds to tag DES2 in CAD
		/// IEC81346 prefix:: '='
		/// </summary>
		public string Designation2 { get; set; }

		/// <summary>
		/// According to IEC81346 = Controlfunction (tree), this should just be seen as an example though we are out for supporting "any" standard so the meaning of this property can change
		/// Corresponds to tag DES3 in CAD
		/// IEC81346 prefix:: '=='
		/// </summary>
		public string Designation3 { get; set; }

		/// <summary>
		/// According to IEC81346 = TopologicalBuildingstructure (tree), this should just be seen as an example though we are out for supporting "any" standard so the meaning of this property can change
		/// Corresponds to tag DES4 in CAD
		/// IEC81346 prefix:: '++'
		/// </summary>
		public string Designation4 { get; set; }

		//public string Name
		//{
		//	get => _name;
		//	set => SetPropertyValue(nameof(Name), ref _name, value);
		//}
		//private string _name;

		[Association("Project-Objects")]
		public Project Project
		{
			get => _project;
			set => SetPropertyValue(nameof(Project), ref _project, value);
		}

		[Association("Sheet-Objects")]
		public Sheet? Sheet
		{
			get => _sheet;
			set => SetPropertyValue(nameof(Sheet), ref _sheet, value);
		}

		[Association("Location-Objects")]
		public Location? Location
		{
			get => _location;
			set => SetPropertyValue(nameof(Location), ref _location, value);
		}

		[Association("Function-Objects")]
		public Function Function
		{
			get => _function;
			set => SetPropertyValue(nameof(Function), ref _function, value);
		}

		protected override void OnSaving()
		{
			if (!IsDeleted && Project == null)
				throw new InvalidOperationException("Ett objekt måste tillhöra ett projekt.");

			base.OnSaving();
		}
	}

}