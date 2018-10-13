
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;
using System.Linq;
using System.Text;

using System.Configuration;
using System.IO;
using System.IO.Ports;
using Unity;

public class EnhanceScrollView : MonoBehaviour
{
    public enum InputSystemType
    {
        NGUIAndWorldInput, // use EnhanceScrollViewDragController.cs to get the input(keyboard and touch)

        UGUIInput,         // use UDragEnhanceView for each item to get drag event
    }

    // Input system type(NGUI or 3d world, UGUI)
    public InputSystemType inputType = InputSystemType.NGUIAndWorldInput;
    // Control the item's scale curve
    public AnimationCurve scaleCurve;
    // Control the position curve
    public AnimationCurve positionCurve;
    // Control the "depth"'s curve(In 3d version just the Z value, in 2D UI you can use the depth(NGUI))
    // NOTE:
    // 1. In NGUI set the widget's depth may cause performance problem
    // 2. If you use 3D UI just set the Item's Z position
    public AnimationCurve depthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));


  //  public GameObject clone;

    // The start center index
    [Tooltip("The Start center index")]
    public int startCenterIndex = 0;
    // Offset width between item
    public float cellWidth = 10f;


    private float totalHorizontalWidth = 500.0f;
    // vertical fixed position value 

    public float yFixedPositionValue = 46.0f;                   // 这是做啥的呢 

    // Lerp duration



    public float lerpDuration = 0.2f;         
    private float mCurrentDuration = 0.0f;              // 控制时间的 
    private int mCenterIndex = 0;
    public bool enableLerpTween = true;

    // center and preCentered item
    public EnhanceItem curCenterItem;                                // private  应该是这个是当前的  object     但是不是方法  
    private EnhanceItem preCenterItem;




    // if we can change the target item
    private bool canChangeItem = true;
    private float dFactor = 0.2f;




    // originHorizontalValue Lerp to horizontalTargetValue
    private float originHorizontalValue = 0.1f;         
    public float curHorizontalValue = 0.5f;

    // "depth" factor (2d widget depth or 3d Z value)
    private int depthFactor = 500;                      // 原来是 5   

    // Drag enhance scroll view
    [Tooltip("Camera for drag ray cast")]
    public Camera sourceCamera;
    private EnhanceScrollViewDragController dragController; // 

    /// <summary>
    /// 
    /// hanfeng  
    /// 
    /// </summary>

                                                               /// <summary>
                                                                                /// 
                                                                                /// </summary>
    public byte[] gest = new byte[9];
    public string get_s;
    public bool m = false;

    //  public Thread m_Thread = null;
    //   static System.IO.Ports.SerialPort serialPort = new SerialPort();



    //  public class SerialPort {
    public String Serial_Init()  //在你的main里面调用他 
    {       //serialPort 这个类在哪里    我不熟悉串口   应该是需要自己定义吧
            //shishi  ok
        serialPort.BaudRate = 230400;
        serialPort.ReadTimeout = 50;        // 设置接受超时 
        serialPort.PortName = "COM3";     // 这里报错 
        serialPort.Open();
        return "0";
    }


    public Thread m_Thread = null;
    static System.IO.Ports.SerialPort serialPort = new SerialPort();





    public void EnableDrag(bool isEnabled)
    {
        if (isEnabled)
        {
            if (inputType == InputSystemType.NGUIAndWorldInput)          
            {
                if (sourceCamera == null)
                {
                    Debug.LogError("## Source Camera for drag scroll view is null ##");
                    return;
                }

                if (dragController == null)
                    dragController = gameObject.AddComponent<EnhanceScrollViewDragController>();// gameObject.AddComponent 添加这个 Controller 
                dragController.enabled = true;
                // set the camera and mask
                dragController.SetTargetCameraAndMask(sourceCamera, (1 << LayerMask.NameToLayer("UI")));  // 添加NameToLayer 
            }
        }
        else
        {
            if (dragController != null)
                dragController.enabled = false;                                 
        }
    }




    // targets enhance item in scroll view
    public List<EnhanceItem> listEnhanceItems;
    // sort to get right index
    private List<EnhanceItem> listSortedItems = new List<EnhanceItem>();

    private static EnhanceScrollView instance;
    public static EnhanceScrollView GetInstance
    {
        get { return instance; }
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        canChangeItem = true;
        int count = listEnhanceItems.Count;
        dFactor = (Mathf.RoundToInt((1f / count) * 10000f)) * 0.0001f;
        mCenterIndex = count / 2;
        if (count % 2 == 0)
            mCenterIndex = count / 2 - 1;
        int index = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            listEnhanceItems[i].CurveOffSetIndex = i;
            listEnhanceItems[i].CenterOffSet = dFactor * (mCenterIndex - index);
            listEnhanceItems[i].SetSelectState(false);
            GameObject obj = listEnhanceItems[i].gameObject;



            if (inputType == InputSystemType.NGUIAndWorldInput)
            {
                DragEnhanceView script = obj.GetComponent<DragEnhanceView>();
                if (script != null)
                    script.SetScrollView(this);
            }
            else
            {
                UDragEnhanceView script = obj.GetComponent<UDragEnhanceView>();
                if (script != null)
                    script.SetScrollView(this);
            }
            index++;
        }

        // set the center item with startCenterIndex
        if (startCenterIndex < 0 || startCenterIndex >= count)
        {
            Debug.LogError("## startCenterIndex < 0 || startCenterIndex >= listEnhanceItems.Count  out of index ##");
            startCenterIndex = mCenterIndex;
        }

        // sorted items
        listSortedItems = new List<EnhanceItem>(listEnhanceItems.ToArray());
        totalHorizontalWidth = cellWidth * count;
        curCenterItem = listEnhanceItems[startCenterIndex];
        curHorizontalValue = 0.5f - curCenterItem.CenterOffSet;
        LerpTweenToTarget(0f, curHorizontalValue, false);

        // 
        // enable the drag actions
        // 
        EnableDrag(true);
    }

    private void LerpTweenToTarget(float originValue, float targetValue, bool needTween = false)
    {
        if (!needTween)
        {
            SortEnhanceItem();
            originHorizontalValue = targetValue;
            UpdateEnhanceScrollView(targetValue);
            this.OnTweenOver();
        }
        else
        {
            originHorizontalValue = originValue;
            curHorizontalValue = targetValue;
            mCurrentDuration = 0.0f;
        }
        enableLerpTween = needTween;
    }

    public void DisableLerpTween()
    {
        this.enableLerpTween = false;
    }

    /// 
    /// Update EnhanceItem state with curve fTime value
    /// 
    public void UpdateEnhanceScrollView(float fValue)
    {
        for (int i = 0; i < listEnhanceItems.Count; i++)
        {
            EnhanceItem itemScript = listEnhanceItems[i];
            float xValue = GetXPosValue(fValue, itemScript.CenterOffSet);
            float scaleValue = GetScaleValue(fValue, itemScript.CenterOffSet);
            float depthCurveValue = depthCurve.Evaluate(fValue + itemScript.CenterOffSet);
            itemScript.UpdateScrollViewItems(xValue, depthCurveValue, depthFactor, listEnhanceItems.Count, yFixedPositionValue, scaleValue);
        }
    }

    void Update()
    {

        ///string s = "";
        string s = "";
        if (enableLerpTween)
            TweenViewToTarget();


        ///      hanfeng   ///


        if (!m)          // 如果m 刚开始不是0（因为开始是0 则true） 则 开始调用函数 
        {

            Serial_Init();
            m = true;

        }

        //serialPort.DiscardOutBuffer();
        /*
        if (serialPort.Read(gest, 0, 8) != 0)                            // gest 读进来   
        {
            string s = "";
            //if(sizeof(gest) == 7)
            //    Debug.Log(gest); 
            s = System.Text.Encoding.UTF8.GetString(gest, 0, 8);
            Debug.Log(s);
            serialPort.DiscardOutBuffer();

        }
        */
        while (s.Length < 7)
        {
            char character = (char)serialPort.ReadChar();             /// /// 
            s += character.ToString();
        }
        Debug.Log(s);

        serialPort.DiscardOutBuffer();

if ((s.Contains("#-3333"))&&(GameObject.Find("clone") == null))                     // 向左转 
        {
            // orderListy.Add(Order.TurnLeft);
            //  SmoothMenuRotationAnimation(true);
            //Debug.Log(97987978);
            // 执行OnBtnLeftClick 
            OnBtnRightClick();
            //  Debug.Log(s);
        }


if (s.Contains("!#-4444"))
        {
            // orderListy.Add(Order.TurnRight);
            // SmoothMenuRotationAnimation(false);

            OnBtnLeftClick();
        }

if (GameObject.Find("clone") == null)
        {
            if (s.Contains("!#-1111"))
            {
                //   GameObject.Find("/fuzi");
                //   if (gameobject.Find == ""    
                //   obj.scale +=...
                // if() 
                //  transform.localScale -= new Vector3(1, 1, 0);
                transform.localScale = new Vector3(0, 0, 0);   //背景消失 

                //Instantiate(GameObject.Find("fuzi"), new Vector3(0, 0, 0), Quaternion.identity); // 这里应该是 curCenterItem

                //  GameObject EnhanceScrollView = Instantiate(GameObject.Find("fuzi"), new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
                //  EnhanceItem curCenterItem

                //     GameObject EnhanceScrollView = Instantiate(EnhanceItem.curCenterItem, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
                GameObject Show = Instantiate(instance.curCenterItem.gameObject, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
				Show.transform.parent = GameObject.Find("Show").transform;

				Show.transform.localPosition = new Vector3 (-7.0f, 46.0f, 0);
                // = GameObject.Instantiate(oklable.gameObject.Vector3.zero, Quaternion.identity) as GameObject;
                Show.name = "clone";    
			// 	GameObject.Find("clone").transform.position = new Vector3 (0.021875f, 0.08125f, 0);    //  (-7, 26, 0);
				 
                
                //   Gameobject missileCopy = Instantiate<Missile>(missile);
                // GameObject.Find("fuzi").transform.localScale = new Vector3(0, 0, 0);
                Show.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);



				GameObject.Find ("LeftArrow").SetActive (false);
				GameObject.Find ("RightArrow").SetActive (false);
				GameObject.Find ("DoubleArrow").SetActive (false);
				GameObject.Find ("BtnLeft").SetActive (false);
				GameObject.Find ("BtnRight").SetActive (false);
				GameObject.Find ("BtnDoubleTap").SetActive (false);

				GameObject root7 = GameObject.Find("UI Root");
				GameObject map7 = root7.transform.Find("BtnSingle").gameObject;

				map7.SetActive(true);


				GameObject root8 = GameObject.Find("UI Root");
				GameObject map8 = root8.transform.Find("SingleArrow").gameObject;

				map8.SetActive(true);

            }

        }

		if ((GameObject.Find("clone")!= null)&&(s.Contains("!#-2222"))) 

			{

      //      if (s.Contains("!#-2222"))
				//       {
                   transform.localScale += new Vector3(1, 1, 1);   // 复原 

                // for(int i = 0; i < GameObject.Find("Show").transform.childCount; i++)
                
                    //     GameObject go = GameObject.Find("Show").transform.GetChild(i).gameObject;

                    Destroy(GameObject.Find("clone"));


					GameObject root1 = GameObject.Find("UI Root");
					GameObject map1 = root1.transform.Find("LeftArrow").gameObject;

					map1.SetActive(true);
					// GameObject.Find ("UI Root/LeftArrow").SetActive (true);

					GameObject root2 = GameObject.Find("UI Root");
					GameObject map2 = root2.transform.Find("RightArrow").gameObject;

					map2.SetActive(true);


					GameObject root3 = GameObject.Find("UI Root");
					GameObject map3 = root3.transform.Find("DoubleArrow").gameObject;

					map3.SetActive(true);

					GameObject root4 = GameObject.Find("UI Root");
					GameObject map4 = root4.transform.Find("BtnLeft").gameObject;

					map4.SetActive(true);

					GameObject root5 = GameObject.Find("UI Root");
					GameObject map5 = root5.transform.Find("BtnRight").gameObject;

					map5.SetActive(true);

					GameObject root6 = GameObject.Find("UI Root");
					GameObject map6 = root6.transform.Find("BtnDoubleTap").gameObject;

					map6.SetActive(true);
				
					GameObject root9 = GameObject.Find("UI Root");
					GameObject map9 = root9.transform.Find("BtnSingle").gameObject;

					map9.SetActive(false);


					GameObject root10 = GameObject.Find("UI Root");
					GameObject map10 = root10.transform.Find("SingleArrow").gameObject;

					map10.SetActive(false);
			           
                                  }
            
}

		
    private void TweenViewToTarget()
    {
        mCurrentDuration += Time.deltaTime;
        if (mCurrentDuration > lerpDuration)
            mCurrentDuration = lerpDuration;

        float percent = mCurrentDuration / lerpDuration;
        float value = Mathf.Lerp(originHorizontalValue, curHorizontalValue, percent);
        UpdateEnhanceScrollView(value);
        if (mCurrentDuration >= lerpDuration)
        {
            canChangeItem = true;
            enableLerpTween = false;
            OnTweenOver();
        }
    }

    private void OnTweenOver()
    {
        if (preCenterItem != null)
            preCenterItem.SetSelectState(false);
        if (curCenterItem != null)
            curCenterItem.SetSelectState(true);
    }

    // Get the evaluate value to set item's scale
    private float GetScaleValue(float sliderValue, float added)
    {
        float scaleValue = scaleCurve.Evaluate(sliderValue + added);
        return scaleValue;
    }

    // Get the X value set the Item's position
    private float GetXPosValue(float sliderValue, float added)
    {
        float evaluateValue = positionCurve.Evaluate(sliderValue + added) * totalHorizontalWidth;
        return evaluateValue;
    }

    private int GetMoveCurveFactorCount(EnhanceItem preCenterItem, EnhanceItem newCenterItem)
    {
        SortEnhanceItem();
        int factorCount = Mathf.Abs(newCenterItem.RealIndex) - Mathf.Abs(preCenterItem.RealIndex);
        return Mathf.Abs(factorCount);
    }

    // sort item with X so we can know how much distance we need to move the timeLine(curve time line)
    static public int SortPosition(EnhanceItem a, EnhanceItem b) { return a.transform.localPosition.x.CompareTo(b.transform.localPosition.x); }
    private void SortEnhanceItem()
    {
        listSortedItems.Sort(SortPosition);
        for (int i = listSortedItems.Count - 1; i >= 0; i--)
            listSortedItems[i].RealIndex = i;
    }

    public void SetHorizontalTargetItemIndex(EnhanceItem selectItem)
    {
        if (!canChangeItem)
            return;

        if (curCenterItem == selectItem)
            return;

        canChangeItem = false;
        preCenterItem = curCenterItem;
        curCenterItem = selectItem;

        // calculate the direction of moving
        float centerXValue = positionCurve.Evaluate(0.5f) * totalHorizontalWidth;
        bool isRight = false;
        if (selectItem.transform.localPosition.x > centerXValue)
            isRight = true;

        // calculate the offset * dFactor
        int moveIndexCount = GetMoveCurveFactorCount(preCenterItem, selectItem);
        float dvalue = 0.0f;
        if (isRight)
        {
            dvalue = -dFactor * moveIndexCount;
        }
        else
        {
            dvalue = dFactor * moveIndexCount;
        }
        float originValue = curHorizontalValue;
        LerpTweenToTarget(originValue, curHorizontalValue + dvalue, true);
    }



    // Click the right button to select the next item.           // 在这里调用 OnBtnRightClick 和 OnBtnLeftClick 函数 
    public void OnBtnRightClick()
    {
        if (!canChangeItem)
            return;
        int targetIndex = curCenterItem.CurveOffSetIndex + 1;
        if (targetIndex > listEnhanceItems.Count - 1)
            targetIndex = 0;
        SetHorizontalTargetItemIndex(listEnhanceItems[targetIndex]);
    }

    // Click the left button the select next next item.


    public void OnBtnLeftClick()
    {
        if (!canChangeItem)
            return;
        int targetIndex = curCenterItem.CurveOffSetIndex - 1;
        if (targetIndex < 0)
            targetIndex = listEnhanceItems.Count - 1;
        SetHorizontalTargetItemIndex(listEnhanceItems[targetIndex]);
    }

    public float factor = 0.001f;
    // On Drag Move
    public void OnDragEnhanceViewMove(Vector2 delta)
    {
        // In developing
        if (Mathf.Abs(delta.x) > 0.0f)
        {
            curHorizontalValue += delta.x * factor;
            LerpTweenToTarget(0.0f, curHorizontalValue, false);
        }
    }

    // On Drag End
    public void OnDragEnhanceViewEnd()
    {
        // find closed item to be centered
        int closestIndex = 0;
        float value = (curHorizontalValue - (int)curHorizontalValue);
        float min = float.MaxValue;
        float tmp = 0.5f * (curHorizontalValue < 0 ? -1 : 1);
        for (int i = 0; i < listEnhanceItems.Count; i++)
        {
            float dis = Mathf.Abs(Mathf.Abs(value) - Mathf.Abs((tmp - listEnhanceItems[i].CenterOffSet)));
            if (dis < min)
            {
                closestIndex = i;
                min = dis;
            }
        }
        originHorizontalValue = curHorizontalValue;
        float target = ((int)curHorizontalValue + (tmp - listEnhanceItems[closestIndex].CenterOffSet));
        preCenterItem = curCenterItem;
        curCenterItem = listEnhanceItems[closestIndex];
        LerpTweenToTarget(originHorizontalValue, target, true);
        canChangeItem = false;
    }
}