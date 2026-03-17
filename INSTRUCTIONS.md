# BOG.RelayHub

Author: John J Schultz

## Projects in the Solution

### BOG.RelayHub

The MinAPI defining the enpdoints and processing for:
- **Channels**: A name, within which are items in *queues* and *references*.
- **Queue**: For single-use, transient items.  A queue item requires a 
    recipient, which can be specific or the same value for first-come, first-serve.
- **Reference**: A keyed value which is non-transient.  It remains until explicitly removed.  
    A new value submitted to an existing key overwrites the existing value.
- **Executive**: For listing and removing channels in bulk, and triggering 
    a shutdown (or a service restart) of the app.

### BOG.Relay.Client

Classes for a client application's use to communicate with the API.

### BOG.Relay.Common

Classes common to the app and client projects.

### BOG.Relay.ConsumerDemo

Demonstrates the endpoint usage and flows.  Also a retrogression test tool after changes.

