
namespace Model.User
{
    public class User
    {
        /// <summary>
        /// Unique identifier on user record.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Id for used on user sign in.
        /// </summary>
        public string UserId { get; set; }
        /// <summary>
        /// User name.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// User email.
        /// </summary>
        public string Email { get; set; }
    }
}