/******************************************************************************\
* Copyright (C) 2012-2016 Leap Motion, Inc. All rights reserved.               *
* Leap Motion proprietary and confidential. Not for distribution.              *
* Use subject to the terms of the Leap Motion SDK Agreement available at       *
* https://developer.leapmotion.com/sdk_agreement, or another agreement         *
* between Leap Motion and you, your company or other organization.             *
\******************************************************************************/

namespace LeapInternal
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Runtime.InteropServices;


    using Leap;

    public class ServiceFrameFactory
    {

        public Frame makeFrame (ref LEAP_TRACKING_EVENT trackingMsg)
        {
            Frame newFrame = new Leap.Frame ((long)trackingMsg.info.frame_id, 
                                 (long)trackingMsg.info.timestamp, 
                                 trackingMsg.framerate,
                                 new InteractionBox (trackingMsg.interaction_box_center.ToLeapVector (), 
                                     trackingMsg.interaction_box_size.ToLeapVector ()));

            int handStructSize = Marshal.SizeOf (typeof(LEAP_HAND));
            int pHandArrayOffset = 0;
            for (int h = 0; h < trackingMsg.nHands; h++) {
                //TODO verify pointer arithmetic is valid on both 32 and 64 bit platforms
                LEAP_HAND hand = LeapC.PtrToStruct<LEAP_HAND> (new IntPtr (trackingMsg.pHands.ToInt64 () + pHandArrayOffset));
                pHandArrayOffset += handStructSize;

                Hand newHand = makeHand (ref hand, newFrame);
                newFrame.AddHand (newHand);
            }
            return newFrame;
        }

        public Image makeImage (ref LEAP_IMAGE_COMPLETE_EVENT imageMsg, ImageData pendingImageData, DistortionData distortionData)
        {
            LEAP_IMAGE_PROPERTIES props = LeapC.PtrToStruct<LEAP_IMAGE_PROPERTIES> (imageMsg.properties);
            Image.ImageType apiImageType;
            switch (props.type) {
                case eLeapImageType.eLeapImageType_Default:
                    apiImageType = Image.ImageType.DEFAULT;
                    break;
                case eLeapImageType.eLeapImageType_Raw:
                    apiImageType = Image.ImageType.RAW;
                    break;
                default:
                    apiImageType = Image.ImageType.DEFAULT;
                    break;
            }
            Image.FormatType apiImageFormat;
            switch (props.format) {
                case eLeapImageFormat.eLeapImageType_IR:
                    apiImageFormat = Image.FormatType.INFRARED;
                    break;
                case eLeapImageFormat.eLeapImageType_RGBIr_Bayer:
                    apiImageFormat = Image.FormatType.IBRG;
                    break;
                default:
                    apiImageFormat = Image.FormatType.INFRARED;
                    break;
            }
            Image.PerspectiveType apiPerspectiveType;
            switch (props.perspective) {
                case eLeapPerspectiveType.eLeapPerspectiveType_stereo_left:
                    apiPerspectiveType = Image.PerspectiveType.STEREO_LEFT;
                    break;
                case eLeapPerspectiveType.eLeapPerspectiveType_stereo_right:
                    apiPerspectiveType = Image.PerspectiveType.STEREO_RIGHT;
                    break;
                case eLeapPerspectiveType.eLeapPerspectiveType_mono:
                    apiPerspectiveType = Image.PerspectiveType.MONO;
                    break;
                default:
                    apiPerspectiveType = Image.PerspectiveType.INVALID;
                    break;
            }

            pendingImageData.CompleteImageData (apiImageType,
                apiImageFormat,
                apiPerspectiveType,
                props.bpp,
                props.width,
                props.height,
                imageMsg.info.timestamp,
                imageMsg.info.frame_id,
                .5f,
                .5f,
                .5f / LeapC.DistortionSize,
                .5f / LeapC.DistortionSize,
                distortionData,
                LeapC.DistortionSize,
                imageMsg.matrix_version);
            pendingImageData.unPinHandle (); //Done with pin for unmanaged code
            Image newImage = new Image (pendingImageData); //Create the public API object
            return newImage;
        }

        public TrackedQuad makeQuad (ref LEAP_TRACKED_QUAD_EVENT quadMsg)
        {
            TrackedQuad quad = new TrackedQuad (quadMsg.width,
                                   quadMsg.height,
                                   quadMsg.resolutionX,
                                   quadMsg.resolutionY,
                                   quadMsg.visible,
                                   quadMsg.position.ToLeapVector (),
                                   quadMsg.orientation.ToLeapMatrix (),
                                   quadMsg.info.frame_id,
                                   null);
            return quad;
        }


        public Hand makeHand (ref LEAP_HAND hand, Frame owningFrame)
        {
            LEAP_BONE arm = LeapC.PtrToStruct<LEAP_BONE> (hand.arm);
            Arm newArm = makeArm (ref arm);
            LEAP_PALM palm = LeapC.PtrToStruct<LEAP_PALM> (hand.palm);
            
            Hand newHand = new Hand ((int)owningFrame.Id,
                               (int)hand.id,
                               hand.confidence,
                               hand.grab_strength,
                               hand.pinch_strength,
                               palm.width,
                               hand.type == eLeapHandType.eLeapHandType_Left,
                               hand.visible_time,
                               newArm,
                               new PointableList (),
                               new FingerList (),
                               new Vector (palm.position.x, palm.position.y, palm.position.z),
                               new Vector (palm.stabilized_position.x, palm.stabilized_position.y, palm.stabilized_position.z),
                               new Vector (palm.velocity.x, palm.velocity.y, palm.velocity.z),
                               new Vector (palm.normal.x, palm.normal.y, palm.normal.z),
                               new Vector (palm.direction.x, palm.direction.y, palm.direction.z),
                               newArm.NextJoint //wrist position
                           );
            LEAP_DIGIT thumbDigit = LeapC.PtrToStruct<LEAP_DIGIT> (hand.thumb);
            Finger thumb = makeFinger (owningFrame, ref hand, ref thumbDigit, Finger.FingerType.TYPE_THUMB);
            newHand.Pointables.Add ((Pointable)thumb);
            newHand.Fingers.Add (thumb);
            LEAP_DIGIT indexDigit = LeapC.PtrToStruct<LEAP_DIGIT> (hand.index);
            Finger index = makeFinger (owningFrame, ref hand, ref indexDigit, Finger.FingerType.TYPE_INDEX);
            newHand.Fingers.Add (index);
            newHand.Pointables.Add ((Pointable)index);
            LEAP_DIGIT middleDigit = LeapC.PtrToStruct<LEAP_DIGIT> (hand.middle);
            Finger middle = makeFinger (owningFrame, ref hand, ref middleDigit, Finger.FingerType.TYPE_MIDDLE);
            newHand.Fingers.Add (middle);
            newHand.Pointables.Add ((Pointable)middle);
            LEAP_DIGIT ringDigit = LeapC.PtrToStruct<LEAP_DIGIT> (hand.ring);
            Finger ring = makeFinger (owningFrame, ref hand, ref ringDigit, Finger.FingerType.TYPE_RING);
            newHand.Fingers.Add (ring);
            newHand.Pointables.Add ((Pointable)ring);
            LEAP_DIGIT pinkyDigit = LeapC.PtrToStruct<LEAP_DIGIT> (hand.pinky);
            Finger pinky = makeFinger (owningFrame, ref hand, ref pinkyDigit, Finger.FingerType.TYPE_PINKY);
            newHand.Fingers.Add (pinky);
            newHand.Pointables.Add ((Pointable)pinky);

            return newHand;
        }

        public Finger makeFinger (Frame owner, ref LEAP_HAND hand, ref LEAP_DIGIT digit, Finger.FingerType type)
        {
            Bone metacarpal = makeBone (ref digit.metacarpal, Bone.BoneType.TYPE_METACARPAL);
            Bone proximal = makeBone (ref digit.proximal, Bone.BoneType.TYPE_PROXIMAL);
            Bone intermediate = makeBone (ref digit.intermediate, Bone.BoneType.TYPE_INTERMEDIATE);
            Bone distal = makeBone (ref digit.distal, Bone.BoneType.TYPE_DISTAL);
            return new Finger ((int)owner.Id,
                (int)hand.id,
                (int)digit.finger_id,
                hand.visible_time,
                distal.NextJoint,
                new Vector (digit.tip_velocity.x, digit.tip_velocity.y, digit.tip_velocity.z),
                intermediate.Direction,
                new Vector (digit.stabilized_tip_position.x, digit.stabilized_tip_position.y, digit.stabilized_tip_position.z),
                intermediate.Width,
                proximal.Length + intermediate.Length + (distal.Length * 0.77f), //0.77 is used in platform code for this calculation
                digit.is_extended,
                type,
                metacarpal,
                proximal,
                intermediate,
                distal
            );
        }

        public Bone makeBone (ref LEAP_BONE bone, Bone.BoneType type)
        {
            Vector prevJoint = new Vector (bone.prev_joint.x, bone.prev_joint.y, bone.prev_joint.z);
            Vector nextJoint = new Vector (bone.next_joint.x, bone.next_joint.y, bone.next_joint.z);
            Vector center = (nextJoint + prevJoint) * .5f;
            float length = (nextJoint - prevJoint).Magnitude;
            Vector direction = (nextJoint - prevJoint) / length;
            Matrix basis = new Matrix (bone.basis.x_basis.x,
                               bone.basis.x_basis.y,
                               bone.basis.x_basis.z,
                               bone.basis.y_basis.x,
                               bone.basis.y_basis.y,
                               bone.basis.y_basis.z,
                               bone.basis.z_basis.x,
                               bone.basis.z_basis.y,
                               bone.basis.z_basis.z);
            return new Bone (prevJoint, nextJoint, center, direction, length, bone.width, type, basis);
        }

        public Arm makeArm (ref LEAP_BONE bone)
        {
            Vector prevJoint = new Vector (bone.prev_joint.x, bone.prev_joint.y, bone.prev_joint.z);
            Vector nextJoint = new Vector (bone.next_joint.x, bone.next_joint.y, bone.next_joint.z);
            Vector center = (nextJoint + prevJoint) * .5f;
            float length = (nextJoint - prevJoint).Magnitude;
            Vector direction = Vector.Zero;
            if (length > 0)
                direction = (nextJoint - prevJoint) / length;
            Matrix basis = new Matrix (bone.basis.x_basis.x,
                               bone.basis.x_basis.y,
                               bone.basis.x_basis.z,
                               bone.basis.y_basis.x,
                               bone.basis.y_basis.y,
                               bone.basis.y_basis.z,
                               bone.basis.z_basis.x,
                               bone.basis.z_basis.y,
                               bone.basis.z_basis.z);
            return new Arm (prevJoint, nextJoint, center, direction, length, bone.width, Bone.BoneType.TYPE_METACARPAL, basis); //Bone type ignored for arms
        }
    }
}
