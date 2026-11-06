using UnityEngine;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Something that can be picked up and carried (e.g. by Industrial Assets' Crane). A script that moves its object
    /// itself implements it to pause while carried: the carrier calls these on every component of the body it holds.
    /// </summary>
    public interface IGrabbable
    {
        /// <summary>Picked up: stop moving the object yourself; the carrier moves it now. <paramref name="by"/> is what it
        /// hangs from (e.g. a crane's grabber).</summary>
        void OnGrabbed(Transform by);

        /// <summary>Put down: carry on (the object falls or stands where it was let go).</summary>
        void OnReleased(Transform by);
    }
}
