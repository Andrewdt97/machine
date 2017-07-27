﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
#if NET45
using System.Linq;
#endif
using System.Linq.Expressions;
using System.Reflection;

namespace SIL.ObjectModel
{
	public abstract class ObservableObject : INotifyPropertyChanged, INotifyPropertyChanging
	{
		/// <summary>
		/// Occurs after a property value changes.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Provides access to the PropertyChanged event handler to derived classes.
		/// </summary>
		protected PropertyChangedEventHandler PropertyChangedHandler
		{
			get
			{
				return PropertyChanged;
			}
		}

		/// <summary>
		/// Occurs before a property value changes.
		/// </summary>
		public event PropertyChangingEventHandler PropertyChanging;

		/// <summary>
		/// Provides access to the PropertyChanging event handler to derived classes.
		/// </summary>
		protected PropertyChangingEventHandler PropertyChangingHandler
		{
			get
			{
				return PropertyChanging;
			}
		}

		/// <summary>
		/// Verifies that a property name exists in this object. This method
		/// can be called before the property is used, for instance before
		/// calling OnPropertyChanged. It avoids errors when a property name
		/// is changed but some places are missed.
		/// <para>This method is only active in DEBUG mode.</para>
		/// </summary>
		/// <param name="propertyName"></param>
		[Conditional("DEBUG")]
		[DebuggerStepThrough]
		public void VerifyPropertyName(string propertyName)
		{
			Type myType = GetType();
#if NET45
			if (!string.IsNullOrEmpty(propertyName) && myType.GetProperty(propertyName) == null)
			{
				var descriptor = this as ICustomTypeDescriptor;

				if (descriptor != null)
				{
					if (descriptor.GetProperties().Cast<PropertyDescriptor>().Any(property => property.Name == propertyName))
						return;
				}

				throw new ArgumentException("Property not found", propertyName);
			}
#elif NETSTANDARD1_3
			if (!string.IsNullOrEmpty(propertyName) && myType.GetTypeInfo().GetDeclaredProperty(propertyName) == null)
				throw new ArgumentException("Property not found", propertyName);
#endif
		}

		protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
		{
			PropertyChangingEventHandler handler = PropertyChanging;
			if (handler != null)
				handler(this, e);
		}

		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
				handler(this, e);
		}

		/// <summary>
		/// Extracts the name of a property from an expression.
		/// </summary>
		/// <typeparam name="T">The type of the property.</typeparam>
		/// <param name="propertyExpression">An expression returning the property's name.</param>
		/// <returns>The name of the property returned by the expression.</returns>
		/// <exception cref="ArgumentNullException">If the expression is null.</exception>
		/// <exception cref="ArgumentException">If the expression does not represent a property.</exception>
		protected string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
		{
			if (propertyExpression == null)
			{
				throw new ArgumentNullException("propertyExpression");
			}

			var body = propertyExpression.Body as MemberExpression;

			if (body == null)
			{
				throw new ArgumentException("Invalid argument", "propertyExpression");
			}

			var property = body.Member as PropertyInfo;

			if (property == null)
			{
				throw new ArgumentException("Argument is not a property", "propertyExpression");
			}

			return property.Name;
		}

		/// <summary>
		/// Assigns a new value to the property. Then, raises the
		/// PropertyChanged event if needed. 
		/// </summary>
		/// <typeparam name="T">The type of the property that
		/// changed.</typeparam>
		/// <param name="propertyExpression">An expression identifying the property
		/// that changed.</param>
		/// <param name="field">The field storing the property's value.</param>
		/// <param name="newValue">The property's value after the change
		/// occurred.</param>
		/// <returns>True if the PropertyChanged event has been raised,
		/// false otherwise. The event is not raised if the old
		/// value is equal to the new value.</returns>
		protected bool Set<T>(Expression<Func<T>> propertyExpression, ref T field, T newValue)
		{
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;

			string propertyName = GetPropertyName(propertyExpression);

			OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
			field = newValue;
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
			return true;
		}

		/// <summary>
		/// Assigns a new value to the property. Then, raises the
		/// PropertyChanged event if needed. 
		/// </summary>
		/// <typeparam name="T">The type of the property that
		/// changed.</typeparam>
		/// <param name="propertyName">The name of the property that
		/// changed.</param>
		/// <param name="field">The field storing the property's value.</param>
		/// <param name="newValue">The property's value after the change
		/// occurred.</param>
		/// <returns>True if the PropertyChanged event has been raised,
		/// false otherwise. The event is not raised if the old
		/// value is equal to the new value.</returns>
		protected bool Set<T>(string propertyName, ref T field, T newValue)
		{
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;

			VerifyPropertyName(propertyName);

			OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
			field = newValue;
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
			return true;
		}
	}
}
